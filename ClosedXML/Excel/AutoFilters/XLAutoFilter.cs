// Keep this file CodeMaid organised and cleaned
using System;
using System.Linq;

namespace ClosedXML.Excel
{
    using System.Collections.Generic;

    internal class XLAutoFilter : IXLAutoFilter
    {
        private readonly Dictionary<Int32, XLFilterColumn> _columns = new Dictionary<int, XLFilterColumn>();

        public XLAutoFilter()
        {
            Filters = new Dictionary<int, List<XLFilter>>();
        }

        public Dictionary<Int32, List<XLFilter>> Filters { get; private set; }

        #region IXLAutoFilter Members

        public IEnumerable<IXLRangeRow> HiddenRows { get => Range.Rows(r => r.WorksheetRow().IsHidden); }
        public Boolean IsEnabled { get; set; }
        public IXLRange Range { get; set; }
        public Int32 SortColumn { get; set; }
        public Boolean Sorted { get; set; }
        public XLSortOrder SortOrder { get; set; }
        public IEnumerable<IXLRangeRow> VisibleRows { get => Range.Rows(r => !r.WorksheetRow().IsHidden); }
        public IEnumerable<int> ColumnIndicesWithHiddenButtons => _columns.Where(kv => kv.Value.HideButton).Select(kv => kv.Key);

        IXLAutoFilter IXLAutoFilter.Clear()
        {
            return Clear();
        }

        public IXLFilterColumn Column(String column)
        {
            var columnNumber = XLHelper.GetColumnNumberFromLetter(column);
            if (columnNumber < 1 || columnNumber > XLHelper.MaxColumnNumber)
                throw new ArgumentOutOfRangeException(nameof(column), "Column '" + column + "' is outside the allowed column range.");

            return Column(columnNumber);
        }

        public IXLFilterColumn Column(Int32 column)
        {
            if (column < 1 || column > XLHelper.MaxColumnNumber)
                throw new ArgumentOutOfRangeException(nameof(column), "Column " + column + " is outside the allowed column range.");

            if (!_columns.TryGetValue(column, out XLFilterColumn filterColumn))
            {
                filterColumn = new XLFilterColumn(this, column);
                _columns.Add(column, filterColumn);
            }

            return filterColumn;
        }

        public IXLAutoFilter Reapply()
        {
            // Recalculate shown / hidden rows
            var rows = Range.Rows(2, Range.RowCount());
            rows.ForEach(row =>
                row.WorksheetRow().Unhide()
            );

            foreach (IXLRangeRow row in rows)
            {
                var rowMatch = true;

                foreach (var columnIndex in Filters.Keys)
                {
                    var columnFilters = Filters[columnIndex];

                    var columnFilterMatch = true;

                    // If the first filter is an 'Or', we need to fudge the initial condition
                    if (columnFilters.Count > 0 && columnFilters.First().Connector == XLConnector.Or)
                    {
                        columnFilterMatch = false;
                    }

                    foreach (var filter in columnFilters)
                    {
                        var condition = filter.Condition;
                        var isText = filter.Value is String;
                        var isDateTime = filter.Value is DateTime;

                        Boolean filterMatch;

                        if (isText)
                            filterMatch = condition(row.Cell(columnIndex).GetFormattedString());
                        else if (isDateTime)
                            filterMatch = row.Cell(columnIndex).DataType == XLDataType.DateTime &&
                                    condition(row.Cell(columnIndex).GetDateTime());
                        else
                            filterMatch = row.Cell(columnIndex).TryGetValue(out double number) &&
                                    condition(number);

                        if (filter.Connector == XLConnector.And)
                        {
                            columnFilterMatch &= filterMatch;
                            if (!columnFilterMatch) break;
                        }
                        else
                        {
                            columnFilterMatch |= filterMatch;
                            if (columnFilterMatch) break;
                        }
                    }

                    rowMatch &= columnFilterMatch;

                    if (!rowMatch) break;
                }

                if (!rowMatch) row.WorksheetRow().Hide();
            }

            return this;
        }

        IXLAutoFilter IXLAutoFilter.Sort(Int32 columnToSortBy, XLSortOrder sortOrder, Boolean matchCase,
                                                                                                         Boolean ignoreBlanks)
        {
            return Sort(columnToSortBy, sortOrder, matchCase, ignoreBlanks);
        }

        #endregion IXLAutoFilter Members

        public XLAutoFilter Clear()
        {
            if (!IsEnabled) return this;

            IsEnabled = false;
            Filters.Clear();
            foreach (IXLRangeRow row in Range.Rows().Where(r => r.RowNumber() > 1))
                row.WorksheetRow().Unhide();
            return this;
        }

        public XLAutoFilter Set(IXLRangeBase range)
        {
            var firstOverlappingTable = range.Worksheet.Tables.FirstOrDefault(t => t.RangeUsed().Intersects(range));
            if (firstOverlappingTable != null)
                throw new InvalidOperationException($"The range {range.RangeAddress.ToStringRelative(includeSheet: true)} is already part of table '{firstOverlappingTable.Name}'");

            Range = range.AsRange();
            IsEnabled = true;
            return this;
        }

        public XLAutoFilter Sort(Int32 columnToSortBy, XLSortOrder sortOrder, Boolean matchCase, Boolean ignoreBlanks)
        {
            if (!IsEnabled)
                throw new InvalidOperationException("Filter has not been enabled.");

            Range.Range(Range.FirstCell().CellBelow(), Range.LastCell()).Sort(columnToSortBy, sortOrder, matchCase,
                                                                              ignoreBlanks);

            Sorted = true;
            SortOrder = sortOrder;
            SortColumn = columnToSortBy;

            Reapply();

            return this;
        }
    }
}
