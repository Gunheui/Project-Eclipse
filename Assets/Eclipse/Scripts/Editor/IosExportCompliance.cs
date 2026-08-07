#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace Eclipse.EditorTools
{
    /// <summary>iOS 빌드가 만든 Xcode 프로젝트의 Info.plist에 수출 규정 면제 선언을 적는다.</summary>
    public static class IosExportCompliance
    {
        [PostProcessBuild(1)]
        public static void OnPostProcessBuild(BuildTarget target, string pathToBuiltProject)
        {
            if (target != BuildTarget.iOS) return;

            // Unity가 빌드마다 Xcode 프로젝트를 새로 쓰므로 손으로 넣은 키는 남지 않는다.
            // false = HTTPS·StoreKit 등 Apple 프레임워크가 제공하는 면제 암호화만 사용.
            // 세이브 파일 자체 암호화나 외부 암호 라이브러리를 도입하면 이 선언을 다시 검토해야 한다.
            var plistPath = Path.Combine(pathToBuiltProject, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            plist.root.SetBoolean("ITSAppUsesNonExemptEncryption", false);
            plist.WriteToFile(plistPath);
        }
    }
}
#endif
