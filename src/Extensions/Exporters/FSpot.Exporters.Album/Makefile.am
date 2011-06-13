ASSEMBLY = FSpot.Exporters.Album
TARGET = library
LINK = $(REF_FSPOT_EXTENSION_ALBUMEXPORT)
INSTALL_DIR = $(EXTENSIONS_INSTALL_DIR)

SOURCES = FSpot.Exporters.Album/AlbumExport.cs

RESOURCES =  \
	Resources/album_exporter.ui \
	Resources/AlbumExport.addin.xml \
	Resources/DefaultTheme/album.css,DefaultTheme.album.css \
	Resources/DefaultTheme/dark.css,DefaultTheme.dark.css \
	Resources/DefaultTheme/index.xsl,DefaultTheme.index.xsl \
	Resources/DefaultTheme/page.xsl,DefaultTheme.page.xsl

include $(top_srcdir)/build/build.mk
