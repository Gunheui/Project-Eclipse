using UnityEngine;
using UnityEngine.UI;

namespace Eclipse.View
{
    /// <summary>
    /// GridLayoutGroup의 셀 크기를 자기 rect를 정확히 채우도록 다시 계산한다. 열 수는
    /// FixedColumnCount 제약의 constraintCount를 쓰고, 줄 수는 자식 개수에서 얻는다.
    /// 스크롤 없이 한 화면에 다 보여야 하는 격자용이다.
    /// 인스펙터의 cellSize는 편집 중 미리보기 값일 뿐 실행 중에는 덮어쓴다.
    /// </summary>
    [RequireComponent(typeof(GridLayoutGroup))]
    public class GridCellAutoSizer : MonoBehaviour
    {
        private GridLayoutGroup _grid;

        private void Awake()
        {
            _grid = GetComponent<GridLayoutGroup>();
            Apply();
        }

        private void OnRectTransformDimensionsChange()
        {
            // 레이아웃 재계산 중 Awake보다 먼저 호출될 수 있다.
            if (_grid != null)
                Apply();
        }

        private void Apply()
        {
            int columns = _grid.constraintCount;
            int rows = Mathf.CeilToInt(transform.childCount / (float)columns);
            if (columns <= 0 || rows <= 0)
                return;

            // 가로세로를 각각 채운다. 한 축만 맞추면 반대 축에 빈 띠가 남거나 칸이 rect 밖으로 밀려난다.
            var rect = ((RectTransform)transform).rect;
            var size = new Vector2(
                Mathf.Max(0f, (rect.width - _grid.padding.horizontal - _grid.spacing.x * (columns - 1)) / columns),
                Mathf.Max(0f, (rect.height - _grid.padding.vertical - _grid.spacing.y * (rows - 1)) / rows));

            if (_grid.cellSize != size)
                _grid.cellSize = size;
        }
    }
}
