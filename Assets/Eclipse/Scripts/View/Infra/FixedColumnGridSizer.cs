using UnityEngine;
using UnityEngine.UI;

namespace Eclipse.View.Infra
{
    /// <summary>
    /// GridLayoutGroup의 cellSize를 폭에 맞춰 재계산해 고정 열 수를 항상 유지한다.
    /// 열 수는 GridLayoutGroup의 FixedColumnCount(constraintCount)를 사용하며,
    /// 화면 비율이 바뀌어도 열 수는 그대로 두고 셀 크기만 조정한다.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(GridLayoutGroup))]
    public class FixedColumnGridSizer : MonoBehaviour
    {
        [Tooltip("셀 세로/가로 비율. 1.05 = 살짝 세로가 긴 카드.")]
        [SerializeField] private float cellAspect = 1.05f;

        [Tooltip("폭 기준이 될 RectTransform. 비우면 자기 자신을 사용한다.")]
        [SerializeField] private RectTransform sizingArea;

        private GridLayoutGroup _grid;
        private RectTransform _rt;

        private void OnEnable()
        {
            Cache();
            Apply();
        }

        private void OnRectTransformDimensionsChange()
        {
            Apply();
        }

        private void Cache()
        {
            if (_grid == null) _grid = GetComponent<GridLayoutGroup>();
            if (_rt == null) _rt = (RectTransform)transform;
        }

        private void Apply()
        {
            Cache();
            if (_grid == null) return;

            int columns = Mathf.Max(1, _grid.constraintCount);
            RectTransform area = sizingArea != null ? sizingArea : _rt;
            float width = area.rect.width;
            if (width <= 0f) return;

            float padding = _grid.padding.left + _grid.padding.right;
            float spacing = _grid.spacing.x * (columns - 1);
            float cellWidth = (width - padding - spacing) / columns;
            if (cellWidth <= 0f) return;

            var next = new Vector2(cellWidth, cellWidth * cellAspect);
            if (_grid.cellSize != next)
                _grid.cellSize = next;
        }
    }
}
