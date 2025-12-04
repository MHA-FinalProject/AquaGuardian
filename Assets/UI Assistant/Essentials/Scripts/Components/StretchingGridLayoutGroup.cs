using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UIAssistant
{
    [HelpURL("https://sites.google.com/view/uiassistant/documentation/scaling?authuser=0#h.2c7qc1ire4b6")]
    [AddComponentMenu("UI Assistant/Stretching Grid Layout Group"), DisallowMultipleComponent]
    public class StretchingGridLayoutGroup : GridLayoutGroup, IScaleable
    {
        #region Variables
        [SerializeField] ScaleSettings ScaleSettings;

        [SerializeField] Vector2 OriginalCellSize;
        [SerializeField] Vector2 OriginalSpacing;
        [SerializeField] RectOffset OriginalPadding = new();

        [Tooltip("Padding, spacing and cell size will be multiplied based on the active Scale Profile.")] public bool EnableScaling;
        [Tooltip("Expand cells to match the Rect Transform's boundaries, minus padding and spacing.")] public bool StretchCells;
        [Tooltip("A single cell's preferred size the layout will try to match.")] public Vector2 PreferredCellSize;
        [Tooltip("A mode used to stretch cells.")] public ExtendedConstraint StretchingConstraint;
        [Tooltip("The number of columns and rows.")] public Vector2Int ConstraintCountVector2;
        [Range(0, 1)] public float MatchWidthOrHeight = .5f;

        [SerializeField] int ColumnCount;
        [SerializeField] int RowCount;
        #endregion

        #region Enums
        public enum ExtendedConstraint
        {
            Flexible = 0,
            Expanded = 1,
            FixedCellCount = 2,
            FixedColumnCount = 3,
            FixedRowCount = 4,
            FixedRowAndColumnCount = 5,
        }
        #endregion

        #region Function
        protected override void OnEnable()
        {
            base.OnEnable();
            
            if (Application.isPlaying && EnableScaling)
            {
                ScaleSettings.Add(this);
                SetScale();
            }
        }
        protected override void OnDisable()
        {
            base.OnDisable();

            if (Application.isPlaying && EnableScaling) ScaleSettings.Remove(this);
        }

        public override void SetLayoutVertical()
        {
            base.SetLayoutVertical();
            if (StretchCells) SetCellSize();
        }

        /// <summary>
        /// Updates spacing and padding values, as well as cell size based on the Scale Settings.
        /// </summary>
        public void SetScale()
        {
            spacing = OriginalSpacing * ScaleSettings.ScaleMultiplier;
            padding.left = Mathf.RoundToInt(OriginalPadding.left * ScaleSettings.ScaleMultiplier);
            padding.right = Mathf.RoundToInt(OriginalPadding.right * ScaleSettings.ScaleMultiplier);
            padding.top = Mathf.RoundToInt(OriginalPadding.top * ScaleSettings.ScaleMultiplier);
            padding.bottom = Mathf.RoundToInt(OriginalPadding.bottom * ScaleSettings.ScaleMultiplier);

            if (StretchCells) SetCellSize();
            else cellSize = OriginalCellSize * ScaleSettings.ScaleMultiplier;
        }

        void SetCellSize()
        {
            float multiplier = Application.isPlaying && EnableScaling ? ScaleSettings.ScaleMultiplier : 1;

            Vector2 preferredSize = PreferredCellSize;
            Vector2 size = new(0, 0);
            
            switch (StretchingConstraint)
            {
                case ExtendedConstraint.Flexible:
                    size.x = CellWidth(preferredSize.x * multiplier);
                    size.y = CellHeight(preferredSize.y * multiplier);
                    break;
                case ExtendedConstraint.Expanded:
                    size = CellSize(rectChildren.Count);
                    break;
                case ExtendedConstraint.FixedCellCount:
                    size = CellSize(constraintCount);
                    break;
                case ExtendedConstraint.FixedColumnCount:
                    preferredSize.x = AvailableWidth / constraintCount;
                    size.x = CellWidth(preferredSize.x);
                    size.y = CellHeight(preferredSize.y * multiplier);
                    break;
                case ExtendedConstraint.FixedRowCount:
                    preferredSize.y = AvailableHeight / constraintCount;
                    size.x = CellWidth(preferredSize.x * multiplier);
                    size.y = CellHeight(preferredSize.y);
                    break;
                case ExtendedConstraint.FixedRowAndColumnCount:
                    preferredSize.x = AvailableWidth / ConstraintCountVector2.x;
                    preferredSize.y = AvailableHeight / ConstraintCountVector2.y;
                    size.x = CellWidth(preferredSize.x);
                    size.y = CellHeight(preferredSize.y);
                    break;
            }

            cellSize = size;
        }
        #endregion

        #region Helpers
        Vector2 CellSize(int cellCount)
        {
            float rowCount;
            float columnCount;

            float lerpBase = Mathf.Round(Mathf.Sqrt(cellCount));
            float t = Mathf.Abs((MatchWidthOrHeight - .5f) * 2);

            if (MatchWidthOrHeight < .5f)
            {
                columnCount = Mathf.Round(Mathf.Lerp(lerpBase, 1, t));
                rowCount = Mathf.Ceil(cellCount / columnCount);
            }
            else
            {
                rowCount = Mathf.Round(Mathf.Lerp(lerpBase, 1, t));
                columnCount = Mathf.Ceil(cellCount / rowCount);
            }

            float preferredWidth = AvailableWidth / columnCount;
            float preferredHeight = AvailableHeight / rowCount;

            float width = CellWidth(preferredWidth);
            float height = CellHeight(preferredHeight);

            ColumnCount = Mathf.RoundToInt(columnCount);
            RowCount = Mathf.RoundToInt(rowCount);
            return new(width, height);
        }
        float CellWidth(float preferredCellWidth)
        {
            float width = rectTransform.rect.width - padding.horizontal;

            int columnCount = Mathf.RoundToInt(width / preferredCellWidth);
            if (columnCount == 0) columnCount = 1;
            else if (columnCount > 1)
            {
                width -= spacing.x * (columnCount - 1);
            }

            ColumnCount = columnCount;
            return width / columnCount;
        }
        float CellHeight(float preferredCellHeight)
        {
            float height = rectTransform.rect.height - padding.vertical;

            int rowCount = Mathf.RoundToInt(height / preferredCellHeight);
            if (rowCount == 0) rowCount = 1;
            else if (rowCount > 1)
            {
                height -= spacing.y * (rowCount - 1);
            }

            RowCount = rowCount;
            return height / rowCount;
        }
        float AvailableWidth
        {
            get
            {
                return rectTransform.rect.width - padding.horizontal;
            }
        }
        float AvailableHeight
        {
            get
            {
                return rectTransform.rect.height - padding.vertical;
            }
        }

        /// <summary>
        /// Returns the maximum number of items that can fit into the layout's bounds.
        /// </summary>
        public int MaxCellCount => ColumnCount * RowCount;

        /// <summary>
        /// Returns the current number of items contained within the layout's bounds.
        /// </summary>
        public int OccupiedCellCount
        {
            get
            {
                int maxCellCount = MaxCellCount;
                if (maxCellCount < rectChildren.Count) return maxCellCount;
                else return rectChildren.Count;
            }
        }

        /// <summary>
        /// Returns the current number of items contained by the layout, including items outside its bounds.
        /// </summary>
        public int OccupiedCellCountUnrestricted => rectChildren.Count;
        #endregion

#if UNITY_EDITOR

        #region Function
        protected override void OnValidate()
        {
            base.OnValidate();

            if (ScaleSettings == null) ScaleSettings = ContentLibrary.GetScaleSettings();

            if (ConstraintCountVector2.x < 1) ConstraintCountVector2.x = 1;
            if (ConstraintCountVector2.y < 1) ConstraintCountVector2.y = 1;

            OriginalCellSize = cellSize;
            OriginalSpacing = spacing;
            OriginalPadding.left = padding.left;
            OriginalPadding.right = padding.right;
            OriginalPadding.top = padding.top;
            OriginalPadding.bottom = padding.bottom;
        }
        #endregion

#endif
    }
}