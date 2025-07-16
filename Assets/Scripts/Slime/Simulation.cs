using ComputeShaderUtility;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using UnityEngine;
using UnityEngine.Experimental.Rendering;


public class Simulation : MonoBehaviour
{
    object _lck = new object();

    public enum SpawnMode { Random, Point, InwardCircle, RandomCircle }

    const int updateKernel = 0;
    const int diffuseMapKernel = 1;
    const int colourKernel = 2;

    public ComputeShader compute;
    public ComputeShader drawAgentsCS;

    public SlimeSettings settings;

    [Header("Display Settings")]
    public bool showAgentsOnly;
    public FilterMode filterMode = FilterMode.Point;
    public GraphicsFormat format = ComputeHelper.defaultGraphicsFormat;


    [SerializeField, HideInInspector] protected RenderTexture trailMap;
    [SerializeField, HideInInspector] protected RenderTexture diffusedTrailMap;
    [SerializeField, HideInInspector] protected RenderTexture displayTexture;
    [SerializeField] protected RenderTexture maskRenderTexture;



    ComputeBuffer agentBuffer;
    ComputeBuffer settingsBuffer;
    Texture2D colourMapTexture;
    [SerializeField] protected Texture2D maskTexture;

    [Header("Python Settings")]
    public string pythonExecutable = "python";      // or "py" on Windows
    public string pythonScriptPath = "Assets/PythonScripts/Test.py";

    Process _pythonProc;
    Thread _readerThread;
    byte[] _currentFrame;
    bool _running = true;


    protected virtual void Start()
    {
        Init();
        transform.GetComponentInChildren<MeshRenderer>().material.mainTexture = displayTexture;
    }


    void Init()
    {
        // 1) Create the mask RenderTexture
        maskRenderTexture = new RenderTexture(settings.width, settings.height, /*depth=*/0, format);
        maskRenderTexture.enableRandomWrite = false;         // read-only use-case
        maskRenderTexture.filterMode = filterMode;
        maskRenderTexture.wrapMode = TextureWrapMode.Clamp;

        // 2) Copy your Texture2D into it
        Graphics.Blit(maskTexture, maskRenderTexture);

        // Create render textures
        ComputeHelper.CreateRenderTexture(ref trailMap, settings.width, settings.height, filterMode, format);
        ComputeHelper.CreateRenderTexture(ref diffusedTrailMap, settings.width, settings.height, filterMode, format);
        ComputeHelper.CreateRenderTexture(ref displayTexture, settings.width, settings.height, filterMode, format);

        // Assign textures
        compute.SetTexture(updateKernel, "TrailMap", trailMap);
        compute.SetTexture(diffuseMapKernel, "TrailMap", trailMap);
        compute.SetTexture(diffuseMapKernel, "DiffusedTrailMap", diffusedTrailMap);
        compute.SetTexture(colourKernel, "ColourMap", displayTexture);
        compute.SetTexture(colourKernel, "TrailMap", trailMap);

        InitSubProcess();

        // Create agents with initial positions and angles
        Agent[] agents = new Agent[settings.numAgents];
        for (int i = 0; i < agents.Length; i++)
        {
            Vector2 centre = new Vector2(settings.width / 2, settings.height / 2);
            Vector2 startPos = Vector2.zero;
            float randomAngle = UnityEngine.Random.value * Mathf.PI * 2;
            float angle = 0;

            if (settings.spawnMode == SpawnMode.Point)
            {
                startPos = centre;
                angle = randomAngle;
            }
            else if (settings.spawnMode == SpawnMode.Random)
            {
                startPos = new Vector2(UnityEngine.Random.Range(0, settings.width), UnityEngine.Random.Range(0, settings.height));
                angle = randomAngle;
            }
            else if (settings.spawnMode == SpawnMode.InwardCircle)
            {
                startPos = centre + UnityEngine.Random.insideUnitCircle * settings.height * 0.5f;
                angle = Mathf.Atan2((centre - startPos).normalized.y, (centre - startPos).normalized.x);
            }
            else if (settings.spawnMode == SpawnMode.RandomCircle)
            {
                startPos = centre + UnityEngine.Random.insideUnitCircle * settings.height * 0.15f;
                angle = randomAngle;
            }

            Vector3Int speciesMask;
            int speciesIndex = 0;
            int numSpecies = settings.speciesSettings.Length;

            if (numSpecies == 1)
            {
                speciesMask = Vector3Int.one;
            }
            else
            {
                int species = UnityEngine.Random.Range(1, numSpecies + 1);
                speciesIndex = species - 1;
                speciesMask = new Vector3Int((species == 1) ? 1 : 0, (species == 2) ? 1 : 0, (species == 3) ? 1 : 0);
            }

            agents[i] = new Agent() { position = startPos, angle = angle, speciesMask = speciesMask, speciesIndex = speciesIndex };
        }

        ComputeHelper.CreateAndSetBuffer<Agent>(ref agentBuffer, agents, compute, "agents", updateKernel);
        compute.SetInt("numAgents", settings.numAgents);
        drawAgentsCS.SetBuffer(0, "agents", agentBuffer);
        drawAgentsCS.SetInt("numAgents", settings.numAgents);


        compute.SetInt("width", settings.width);
        compute.SetInt("height", settings.height);
    }

    void InitSubProcess()
    {
        // 1) Prepare a GPU-side RenderTexture for your compute shader
        ComputeHelper.CreateRenderTexture(ref maskRenderTexture, settings.width, settings.height, filterMode, format);

        compute.SetTexture(diffuseMapKernel, "BackgroundImage", maskRenderTexture);

        // 2) Launch Python once
        var psi = new ProcessStartInfo
        {
            FileName = pythonExecutable,
            Arguments = Path.GetFullPath(pythonScriptPath),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = false
        };

        List<string> envVars = new List<string>();

        _pythonProc = new Process { StartInfo = psi };
        //_pythonProc.OutputDataReceived += (s, e) => { if (e.Data != null) UnityEngine.Debug.Log($"[PY OUT] {e.Data}"); };
        _pythonProc.ErrorDataReceived += (s, e) => { if (e.Data != null) UnityEngine.Debug.LogError($"[PY ERR] {e.Data}"); };
        _pythonProc.Exited += (s, e) => UnityEngine.Debug.Log($"[PY EXIT] code={_pythonProc.ExitCode}");
        _pythonProc.Start();
        //_pythonProc.BeginOutputReadLine();
        _pythonProc.BeginErrorReadLine();
        // 3) Start background thread to read frames
        _readerThread = new Thread(FrameReaderLoop) { IsBackground = true };
        _readerThread.Start();
    }

    public void UpdateBackgroundImage()
    {
        //if(_cnt++ % 30 != 0) return; // Update every 10 frames


        // If a new frame is waiting, dequeue and apply it
        lock(_lck){
            if (_currentFrame != null)
            {
                // Decode into a temporary Texture2D
                maskTexture = new Texture2D(512,512);
                maskTexture.LoadImage(_currentFrame);
                _currentFrame = null;

                Graphics.Blit(maskTexture, maskRenderTexture);
            }
        }
    }

    void FrameReaderLoop()
    {
        var stream = _pythonProc.StandardOutput.BaseStream;
        var reader = new BinaryReader(stream);
        try
        {
            while (_running)
            {
                // Read 4-byte length prefix
                byte[] lenBytes = reader.ReadBytes(4);
                if (lenBytes.Length < 4) break;
                int frameLen = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(lenBytes, 0));

                // Read exactly frameLen bytes
                byte[] pngBytes = reader.ReadBytes(frameLen);
                if (pngBytes.Length < frameLen) break;

                _currentFrame = pngBytes;
            }
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"FrameReaderLoop error: {e}");
        }
    }

    void FixedUpdate()
    {
        for (int i = 0; i < settings.stepsPerFrame; i++)
        {
            RunSimulation();
        }
    }

    void LateUpdate()
    {
        if (showAgentsOnly)
        {
            ComputeHelper.ClearRenderTexture(displayTexture);

            drawAgentsCS.SetTexture(0, "TargetTexture", displayTexture);
            ComputeHelper.Dispatch(drawAgentsCS, settings.numAgents, 1, 1, 0);

        }
        else
        {
            ComputeHelper.Dispatch(compute, settings.width, settings.height, 1, kernelIndex: colourKernel);
            //	ComputeHelper.CopyRenderTexture(trailMap, displayTexture);
        }
    }

    void RunSimulation()
    {
        var speciesSettings = settings.speciesSettings;
        ComputeHelper.CreateStructuredBuffer(ref settingsBuffer, speciesSettings);
        compute.SetBuffer(updateKernel, "speciesSettings", settingsBuffer);
        compute.SetBuffer(colourKernel, "speciesSettings", settingsBuffer);

        // Assign settings
        compute.SetFloat("deltaTime", Time.fixedDeltaTime);
        compute.SetFloat("time", Time.fixedTime);

        compute.SetFloat("trailWeight", settings.trailWeight);
        compute.SetFloat("decayRate", settings.decayRate);
        compute.SetFloat("diffuseRate", settings.diffuseRate);
        compute.SetInt("numSpecies", speciesSettings.Length);

        UpdateBackgroundImage();

        ComputeHelper.Dispatch(compute, settings.numAgents, 1, 1, kernelIndex: updateKernel);
        ComputeHelper.Dispatch(compute, settings.width, settings.height, 1, kernelIndex: diffuseMapKernel);

        ComputeHelper.CopyRenderTexture(diffusedTrailMap, trailMap);
    }

    void OnDestroy()
    {
        ReleaseResources();
    }

    public void ResetSimulation()
    {
        ReleaseResources();
        Init();
        transform.GetComponentInChildren<MeshRenderer>().material.mainTexture = displayTexture;
    }

    void ReleaseResources()
    {
        ComputeHelper.Release(agentBuffer, settingsBuffer);
        if (trailMap != null) trailMap.Release();
        if (diffusedTrailMap != null) diffusedTrailMap.Release();
        if (displayTexture != null) displayTexture.Release();
        if (maskRenderTexture != null) maskRenderTexture.Release();

        _pythonProc?.Kill();
    }

    public struct Agent
    {
        public Vector2 position;
        public float angle;
        public Vector3Int speciesMask;
        int unusedSpeciesChannel;
        public int speciesIndex;
    }


}
