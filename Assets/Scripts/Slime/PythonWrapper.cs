using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using UnityEngine;

namespace Assets.Scripts.Slime
{
    public enum PythonProcessType
    {
        BackgroundImage = 0,
        WeightedOutline = 1,
        WeightedLevelSetOutline = 2,
        WeightedLevelSetOutlineIntertior = 3
    }

    public class PythonWrapper
    {
        private object _lck = new object();
        private readonly string _path;
        private Simulation _sim;
        private Process _pythonProc;
        private Thread _readerThread;
        private bool _running = true;
        private Vector2Int[] _blackCoords;
        private PythonProcessType _processType;
        byte[] _currentFrame;

        string BackGroundImagePath = "Assets/PythonScripts/PythonBackgroundImage.py";
        string WeightedOutlinePath = "Assets/PythonScripts/PythonWeightedOutline.py";
        string WeightedLevelSetOutlinePath = "Assets/PythonScripts/PythonWeightedLevelSetOutline.py";
        string WeightedLevelSetOutlineInteriorPath = "Assets/PythonScripts/PythonWeightedLevelSetOutlineInterior.py";

        public PythonWrapper(PythonProcessType processType, Simulation sim)
        {
            _processType = processType;
            _sim = sim;

            switch (processType)
            {
                case PythonProcessType.BackgroundImage:
                    _path = BackGroundImagePath;
                    break;
                case PythonProcessType.WeightedOutline:
                    _path = WeightedOutlinePath;
                    break;
                case PythonProcessType.WeightedLevelSetOutline:
                    _path = WeightedLevelSetOutlinePath;
                    break;
                case PythonProcessType.WeightedLevelSetOutlineIntertior:
                    _path = WeightedLevelSetOutlineInteriorPath;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(processType), processType, null);
            }
        }

        public void InitProcess(RenderTexture maskRenderTexture)
        {
            // 1) Prepare a GPU-side RenderTexture for your compute shader
            _sim.compute.SetBool("useBackGroundImage", false);
            _sim.compute.SetBool("useBackgroundWeight", false);

            _sim.compute.SetTexture(_sim.colourKernel, "BackGroundImage", maskRenderTexture);
            _sim.compute.SetTexture(_sim.diffuseMapKernel, "BackGroundWeight", maskRenderTexture);

            if (_processType == PythonProcessType.BackgroundImage)
            {
                _sim.compute.SetBool("useBackGroundImage", true);
            }
            else 
            {
                _sim.compute.SetBool("useBackgroundWeight", true);
            }


            // 2) Launch Python once
            var psi = new ProcessStartInfo
            {
                FileName = "py",
                Arguments = Path.GetFullPath(_path),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            _pythonProc = new Process { StartInfo = psi };
            _pythonProc.ErrorDataReceived += (s, e) => { if (e.Data != null) UnityEngine.Debug.LogWarning($"[PY ERR] {e.Data}"); };
            _pythonProc.Exited += (s, e) => UnityEngine.Debug.Log($"[PY EXIT] code={_pythonProc.ExitCode}");
            _pythonProc.Start();
            _pythonProc.BeginErrorReadLine();
            _readerThread = new Thread(FrameReaderLoop) { IsBackground = true };
            _readerThread.Start();
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

        public void UpdateBackgroundImage(RenderTexture maskRenderTexture)
        {
            int w = 640;
            int h = 480;
            lock (_lck)
            {
                //if (_blackCoords != null)
                //{
                //    var tex = new Texture2D(w, h, TextureFormat.R8, false);
                //    var pixels = new Color32[w * h];
                //    foreach (var v in _blackCoords)
                //    {
                //        pixels[(h - v.y - 1) * w + v.x] = new Color32(255, 255, 255, 0);
                //    }
                //    tex.SetPixels32(pixels);
                //    tex.Apply();

                //    _blackCoords = null;

                //    Graphics.Blit(tex, maskRenderTexture);
                //}

                if (_currentFrame != null)
                {
                    var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                    tex.LoadImage(_currentFrame);

                    Graphics.Blit(tex, maskRenderTexture);

                    _currentFrame = null;
                }
            }
        }

        public void Stop()
        {
            _running = false;
            _pythonProc?.Kill();
            _pythonProc?.Dispose();
            _readerThread?.Join();
            _readerThread = null;
            _pythonProc = null;
        }
    }
}