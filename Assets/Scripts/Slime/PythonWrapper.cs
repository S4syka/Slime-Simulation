using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using UnityEngine;

namespace Assets.Scripts.Slime
{
    public class PythonWrapper
    {
        private object _lck = new object();
        private readonly string _path;
        private Simulation _sim;
        private Process _pythonProc;
        private Thread _readerThread;
        private bool _running = true;
        private Vector2Int[] _blackCoords;

        public PythonWrapper(string path, Simulation sim)
        {
            _path = path;
            _sim = sim;
        }

        public void InitProcess(RenderTexture maskRenderTexture)
        {
            // 1) Prepare a GPU-side RenderTexture for your compute shader
            _sim.compute.SetTexture(_sim.diffuseMapKernel, "BackgroundImage", maskRenderTexture);

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
                    uint count = reader.ReadUInt32();
                    var tempBlackCoords = new Vector2Int[count];
                    for (int i = 0; i < count; i++)
                    {
                        ushort x = reader.ReadUInt16();
                        ushort y = reader.ReadUInt16();
                        tempBlackCoords[i] = new Vector2Int(x, y);
                    }
                    _blackCoords = tempBlackCoords;
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
                if (_blackCoords != null)
                {
                    var tex = new Texture2D(w, h, TextureFormat.R8, false);
                    var pixels = new Color32[w * h];
                    foreach (var v in _blackCoords)
                    {
                        pixels[(h - v.y - 1) * w + v.x] = new Color32(255, 255, 255, 0);
                    }
                    tex.SetPixels32(pixels);
                    tex.Apply();

                    _blackCoords = null;

                    Graphics.Blit(tex, maskRenderTexture);
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