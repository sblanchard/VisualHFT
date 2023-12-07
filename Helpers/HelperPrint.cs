using System;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace VisualHFT.Helpers;

internal class ProgramPaginator : DocumentPaginator
{
    private readonly FrameworkElement Element;

    private ProgramPaginator()
    {
    }

    public ProgramPaginator(FrameworkElement element)
    {
        Element = element;
    }

    public override bool IsPageCountValid => true;

    public int Columns => (int)Math.Ceiling(Element.ActualWidth / PageSize.Width);

    public int Rows => (int)Math.Ceiling(Element.ActualHeight / PageSize.Height);

    public override int PageCount => Columns * Rows;

    public override Size PageSize { set; get; }

    public override IDocumentPaginatorSource Source => null;

    public override DocumentPage GetPage(int pageNumber)
    {
        Element.RenderTransform = new TranslateTransform(-PageSize.Width * (pageNumber % Columns),
            -PageSize.Height * (pageNumber / Columns));

        var elementSize = new Size(
            Element.ActualWidth,
            Element.ActualHeight);
        Element.Measure(elementSize);
        Element.Arrange(new Rect(new Point(0, 0), elementSize));

        var page = new DocumentPage(Element);
        Element.RenderTransform = null;

        return page;
    }
}