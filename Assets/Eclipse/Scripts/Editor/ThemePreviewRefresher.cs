using Eclipse.View.Theme;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Eclipse.EditorTools
{
    /// <summary>
    /// 테마 색을 고치면 열린 씬과 프리팹 편집 화면을 실행 없이 다시 칠한다.
    /// 저장된 프리팹은 훑지 않는다. 열어 보는 순간 각자의 OnEnable이 칠한다.
    /// </summary>
    [InitializeOnLoad]
    public static class ThemePreviewRefresher
    {
        private static bool _queued;

        static ThemePreviewRefresher()
        {
            UIThemeSO.Changed -= OnThemeChanged;
            UIThemeSO.Changed += OnThemeChanged;
        }

        private static void OnThemeChanged(UIThemeSO theme)
        {
            // 색 하나를 끌면 알림이 연달아 온다. 인스펙터 갱신이 끝난 뒤 한 번만 칠한다.
            if (_queued) return;
            _queued = true;
            EditorApplication.delayCall += Repaint;
        }

        private static void Repaint()
        {
            _queued = false;

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;

                foreach (var root in scene.GetRootGameObjects())
                    ApplyAll(root);
            }

            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null)
                ApplyAll(stage.prefabContentsRoot);
        }

        private static void ApplyAll(GameObject root)
        {
            foreach (var themed in root.GetComponentsInChildren<ThemedGraphic>(true))
                themed.ApplyTheme();
            foreach (var themed in root.GetComponentsInChildren<ThemedSelectable>(true))
                themed.ApplyTheme();
        }
    }
}
