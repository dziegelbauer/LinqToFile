namespace LinqToFile.Formats.Xlsx;

internal sealed record XlsxMutationLayout(int[] Order, string[] Headers, string Part, string Namespace, XlsxMutationStyles Styles);
