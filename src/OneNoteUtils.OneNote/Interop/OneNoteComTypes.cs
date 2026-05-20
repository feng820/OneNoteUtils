using System.Runtime.InteropServices;

namespace OneNoteUtils.OneNote.Interop;

/// <summary>
/// COM interface for the OneNote Application object.
/// GUID from the OneNote 2016+ type library. Only the methods we use are declared;
/// all others are vtable placeholders to maintain correct slot ordering.
/// </summary>
[ComImport]
[Guid("452AC71A-B655-4967-A208-A4CC39DD7949")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
internal interface IOneNoteApplication
{
    void GetHierarchy(
        [MarshalAs(UnmanagedType.BStr)] string bstrStartNodeID,
        HierarchyScope hsScope,
        [MarshalAs(UnmanagedType.BStr)] out string pbstrHierarchyXmlOut,
        XMLSchema xsSchema = XMLSchema.Current);

    void UpdateHierarchy(
        [MarshalAs(UnmanagedType.BStr)] string bstrChangesXmlIn,
        XMLSchema xsSchema = XMLSchema.Current);

    void OpenHierarchy(
        [MarshalAs(UnmanagedType.BStr)] string bstrPath,
        [MarshalAs(UnmanagedType.BStr)] string bstrRelativeToObjectID,
        [MarshalAs(UnmanagedType.BStr)] out string pbstrObjectID,
        int cftIfNotExist = 0);

    void DeleteHierarchy(
        [MarshalAs(UnmanagedType.BStr)] string bstrObjectID,
        long dateExpectedLastModified = 0,
        [MarshalAs(UnmanagedType.Bool)] bool deletePermanently = false);

    void CreateNewPage(
        [MarshalAs(UnmanagedType.BStr)] string bstrSectionID,
        [MarshalAs(UnmanagedType.BStr)] out string pbstrPageID,
        int npsNewPageStyle = 0);

    void CloseNotebook(
        [MarshalAs(UnmanagedType.BStr)] string bstrNotebookID,
        [MarshalAs(UnmanagedType.Bool)] bool force = false);

    void GetHierarchyParent(
        [MarshalAs(UnmanagedType.BStr)] string bstrObjectID,
        [MarshalAs(UnmanagedType.BStr)] out string pbstrParentID);

    void GetPageContent(
        [MarshalAs(UnmanagedType.BStr)] string bstrPageID,
        [MarshalAs(UnmanagedType.BStr)] out string pbstrPageXmlOut,
        PageInfo pageInfoToExport = PageInfo.Basic,
        XMLSchema xsSchema = XMLSchema.Current);

    void UpdatePageContent(
        [MarshalAs(UnmanagedType.BStr)] string bstrPageChangesXmlIn,
        long dateExpectedLastModified = 0,
        XMLSchema xsSchema = XMLSchema.Current,
        [MarshalAs(UnmanagedType.Bool)] bool force = false);

    void GetBinaryPageContent(
        [MarshalAs(UnmanagedType.BStr)] string bstrPageID,
        [MarshalAs(UnmanagedType.BStr)] string bstrCallbackID,
        [MarshalAs(UnmanagedType.BStr)] out string pbstrBinaryObjectB64Out);

    void DeletePageContent(
        [MarshalAs(UnmanagedType.BStr)] string bstrPageID,
        [MarshalAs(UnmanagedType.BStr)] string bstrObjectID,
        long dateExpectedLastModified = 0,
        [MarshalAs(UnmanagedType.Bool)] bool force = false);

    void NavigateTo(
        [MarshalAs(UnmanagedType.BStr)] string bstrHierarchyObjectID,
        [MarshalAs(UnmanagedType.BStr)] string bstrObjectID,
        [MarshalAs(UnmanagedType.Bool)] bool fNewWindow = false);

    void NavigateToUrl(
        [MarshalAs(UnmanagedType.BStr)] string bstrUrl,
        [MarshalAs(UnmanagedType.Bool)] bool fNewWindow = false);

    void Publish(
        [MarshalAs(UnmanagedType.BStr)] string bstrHierarchyID,
        [MarshalAs(UnmanagedType.BStr)] string bstrTargetFilePath,
        int pfPublishFormat = 0,
        [MarshalAs(UnmanagedType.BStr)] string bstrCLSIDofExporter = "");

    void OpenPackage(
        [MarshalAs(UnmanagedType.BStr)] string bstrPathPackage,
        [MarshalAs(UnmanagedType.BStr)] string bstrPathDest,
        [MarshalAs(UnmanagedType.BStr)] out string pbstrPathOut);

    void GetHyperlinkToObject(
        [MarshalAs(UnmanagedType.BStr)] string bstrHierarchyID,
        [MarshalAs(UnmanagedType.BStr)] string bstrPageContentObjectID,
        [MarshalAs(UnmanagedType.BStr)] out string pbstrHyperlinkOut);

    void FindPages(
        [MarshalAs(UnmanagedType.BStr)] string bstrStartNodeID,
        [MarshalAs(UnmanagedType.BStr)] string bstrSearchString,
        [MarshalAs(UnmanagedType.BStr)] out string pbstrHierarchyXmlOut,
        [MarshalAs(UnmanagedType.Bool)] bool fIncludeUnindexedPages = false,
        [MarshalAs(UnmanagedType.Bool)] bool fDisplay = false,
        XMLSchema xsSchema = XMLSchema.Current);
}

/// <summary>
/// CoClass for creating the OneNote Application COM object.
/// </summary>
[ComImport]
[Guid("D7FAC39E-7FF1-49AA-98CF-A1DDD316337E")]
internal class OneNoteApplicationClass
{
}

internal enum HierarchyScope
{
    Self = 0,
    Children = 1,
    Notebooks = 2,
    Sections = 3,
    Pages = 4,
}

internal enum PageInfo
{
    Basic = 0,
    BinaryData = 1,
    Selection = 2,
    BinaryDataSelection = 3,
    FileType = 4,
    BinaryDataFileType = 5,
    SelectionFileType = 6,
    All = 7,
}

internal enum XMLSchema
{
    OneNote2007 = 0,
    OneNote2010 = 1,
    OneNote2013 = 2,
    Current = 2,
}
