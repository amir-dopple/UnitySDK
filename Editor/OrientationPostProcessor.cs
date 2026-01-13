#if UNITY_ANDROID
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Android;

namespace Dopple.InputSDK.Editor
{
    /// <summary>
    /// Android build post-processor that removes screen orientation constraints from AndroidManifest.xml.
    /// This allows the app to respond to device rotation via gyroscope rather than being locked to a specific orientation.
    /// </summary>
    public class OrientationPostProcessor : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder { get { return 999; } }

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string manifestPath = Path.Combine(path, "src/main/AndroidManifest.xml");

            if (File.Exists(manifestPath))
            {
                string manifestContent = File.ReadAllText(manifestPath);
                string pattern = @"android:screenOrientation\s*=\s*""[^""]*""";
                manifestContent = Regex.Replace(manifestContent, pattern, "");
                File.WriteAllText(manifestPath, manifestContent);

                UnityEngine.Debug.Log("[Dopple.InputSDK] OrientationPostProcessor: Removed android:screenOrientation from AndroidManifest.xml");
            }
            else
            {
                UnityEngine.Debug.LogWarning("[Dopple.InputSDK] OrientationPostProcessor: AndroidManifest.xml not found at " + manifestPath);
            }
        }
    }
}
#endif
