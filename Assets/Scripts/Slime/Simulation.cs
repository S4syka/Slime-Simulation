using Assets.Scripts.Slime;
using ComputeShaderUtility;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;


public class Simulation : MonoBehaviour
{
    public readonly int updateKernel = 0;
    public readonly int diffuseMapKernel = 1;
    public readonly int colourKernel = 2;

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
    [SerializeField, HideInInspector] protected RenderTexture maskRenderTexture;

    [SerializeField] PythonProcessType pythonProcessType = PythonProcessType.WeightedLevelSetOutline;

    ComputeBuffer agentBuffer;
    ComputeBuffer settingsBuffer;
    Texture2D colourMapTexture;
    [SerializeField] protected Texture2D maskTexture;

    //public string pythonScriptPath = "Assets/Scripts/Slime/python_script.py";
    private PythonWrapper _pyWrap;

    protected virtual void Start()
    {
        Init();
        transform.GetComponentInChildren<MeshRenderer>().material.mainTexture = displayTexture;
    }

    void Init()
    {
        if (_pyWrap is null)
        {
            // 1) Create the mask RenderTexture
            maskRenderTexture = new RenderTexture(settings.width, settings.height, /*depth=*/0, format);
            maskRenderTexture.enableRandomWrite = false;         // read-only use-case
            maskRenderTexture.filterMode = filterMode;
            maskRenderTexture.wrapMode = TextureWrapMode.Clamp;

            // 2) Copy your Texture2D into it
            Graphics.Blit(maskTexture, maskRenderTexture);
            ComputeHelper.CreateRenderTexture(ref maskRenderTexture, settings.width, settings.height, filterMode, format);
            _pyWrap = new PythonWrapper(pythonProcessType, this);
            _pyWrap.InitProcess(maskRenderTexture);
        }

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


        Agent[] agents = AgentHelper.GenerateInitialPositions(settings.numAgents, settings.spawnMode, settings.width, settings.height, settings.speciesSettings.Length);

        ComputeHelper.CreateAndSetBuffer<Agent>(ref agentBuffer, agents, compute, "agents", updateKernel);
        compute.SetInt("numAgents", settings.numAgents);
        drawAgentsCS.SetBuffer(0, "agents", agentBuffer);
        drawAgentsCS.SetInt("numAgents", settings.numAgents);

        compute.SetInt("width", settings.width);
        compute.SetInt("height", settings.height);
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

        _pyWrap.UpdateBackgroundImage(maskRenderTexture);

        ComputeHelper.Dispatch(compute, settings.numAgents, 1, 1, kernelIndex: updateKernel);
        ComputeHelper.Dispatch(compute, settings.width, settings.height, 1, kernelIndex: diffuseMapKernel);

        ComputeHelper.CopyRenderTexture(diffusedTrailMap, trailMap);
    }

    void OnDestroy()
    {
        ReleaseResources();
        if (maskRenderTexture != null) maskRenderTexture.Release();
        _pyWrap.Stop();
    }

    public void ResetSimulation()
    {
        ReleaseResources();
        _pyWrap.Stop();
        _pyWrap = null;
        Init();
        transform.GetComponentInChildren<MeshRenderer>().material.mainTexture = displayTexture;
    }

    void ReleaseResources()
    {
        ComputeHelper.Release(agentBuffer, settingsBuffer);
        if (trailMap != null) trailMap.Release();
        if (diffusedTrailMap != null) diffusedTrailMap.Release();
        if (displayTexture != null) displayTexture.Release();
    }
}
