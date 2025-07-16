using System.Diagnostics;
using System.IO;
using UnityEngine;

public class PythonImageLoader : MonoBehaviour
{
    public string pythonExecutable = "py";
    public string scriptPath = "Assets/PythonScripts/Test.py";

    void Start()
    {
        var psi = new ProcessStartInfo
        {
            FileName = pythonExecutable,
            Arguments = $"\"{scriptPath}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };

        var proc = Process.Start(psi);
        using (var ms = new MemoryStream())
        {
            // Copy all bytes from Python’s stdout into a MemoryStream
            proc.StandardOutput.BaseStream.CopyTo(ms);
            proc.WaitForExit();

            byte[] imgBytes = ms.ToArray();
            var tex = new Texture2D(2, 2);
            if (tex.LoadImage(imgBytes))
            {
                // Success! Apply the texture to something in your scene:
                GetComponent<Renderer>().material.mainTexture = tex;
            }
            else
            {
                UnityEngine.Debug.LogError("Failed to decode image bytes");
            }
        }
    }
}
