using UnityEngine;
using UnityEngine.UI;

namespace Eclipse.View
{
    /// <summary>
    /// GridLayoutGroup의 셀 크기를 자기 폭에 맞춰 다시 계산한다. 열 수는 FixedColumnCount 제약의
    /// constraintCount를, 셀 가로세로 비율은 인스펙터에 설정된 cellSize를 기준으로 삼는다.
    /// </summary>
    [RequireComponent(typeof(GridLayoutGroup))]
    public class GridCellAutoSizer : MonoBehaviour
    {
        private GridLayoutGroup _grid;
        private float _heightPerWidth;

        private void Awake()
        {
            _grid = GetComponent<GridLayoutGroup>();
            _heightPerWidth = _grid.cellSize.y / _grid.cellSize.x;
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
            float available = ((RectTransform)transform).rect.width
                - _grid.padding.horizontal - _grid.spacing.x * (columns - 1);
            float width = available / columns;
            var size = new Vector2(width, width * _heightPerWidth);
            if (_grid.cellSize != size)
                _grid.cellSize = size;
        }
    }
}
