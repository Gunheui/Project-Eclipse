using System.IO;
using Eclipse.Presentation;
using UnityEditor;
using UnityEngine;

namespace Eclipse.EditorTools
{
    /// <summary>
    /// 개발용 세이브 파일 조작 메뉴. 플레이 중에는 동작하지 않는다 — 메모리 상태가 다음 저장 때
    /// 파일을 덮어써 조작이 무효가 되기 때문이다.
    /// </summary>
    public static class SaveDevTools
    {
        [MenuItem("Eclipse/세이브/캐릭터 레벨 초기화")]
        private static void ResetCharacterLevels()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("캐릭터 레벨 초기화", "플레이 모드를 멈추고 실행해라.", "확인");
                return;
            }

            string path = SaveService.DefaultFilePath;
            if (!File.Exists(path))
            {
                EditorUtility.DisplayDialog("캐릭터 레벨 초기화", $"세이브 파일이 없다.\n{path}", "확인");
                return;
            }

            var data = SaveService.LoadOrNew(path);
            if (data.owned == null || data.owned.Count == 0)
            {
                EditorUtility.DisplayDialog("캐릭터 레벨 초기화", "보유 캐릭터가 없다.", "확인");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "캐릭터 레벨 초기화",
                    $"보유 캐릭터 {data.owned.Count}명의 레벨을 1로 되돌린다.\n재화·챕터 진행·편성은 그대로다.\n\n{path}",
                    "초기화", "취소"))
                return;

            for (int i = 0; i < data.owned.Count; i++)
            {
                // OwnedEntry는 구조체 — 복사본을 고쳐 되넣어야 리스트에 반영된다.
                var entry = data.owned[i];
                entry.level = 1;
                data.owned[i] = entry;
            }

            File.WriteAllText(path, JsonUtility.ToJson(data));
            Debug.Log($"캐릭터 레벨 초기화 완료 — {data.owned.Count}명, {path}");
        }
    }
}
