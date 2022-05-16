using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace PRM.Controls
{
    public class GridWith4Columns : Grid
    {
        public GridWith4Columns()
        {
            //ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star), SharedSizeGroup = $"{nameof(GridWith4Columns)}_{ColumnDefinitions.Count}" });
            //ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star), SharedSizeGroup = $"{nameof(GridWith4Columns)}_{ColumnDefinitions.Count}" });
            //ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star), SharedSizeGroup = $"{nameof(GridWith4Columns)}_{ColumnDefinitions.Count}" });
            //ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star), SharedSizeGroup = $"{nameof(GridWith4Columns)}_{ColumnDefinitions.Count}" });
            ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto, SharedSizeGroup = $"{nameof(GridWith4Columns)}_{ColumnDefinitions.Count}" });
            ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(300, GridUnitType.Pixel), SharedSizeGroup = $"{nameof(GridWith4Columns)}_{ColumnDefinitions.Count}" });
            ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto, SharedSizeGroup = $"{nameof(GridWith4Columns)}_{ColumnDefinitions.Count}" });
            ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star)});
        }
    }
}
