include $(top_srcdir)/Makefile.include

PLUGIN_NAME = DPAPBrowser

PLUGIN_MANIFEST = $(PLUGIN_NAME).addin.xml

PLUGIN_ASSEMBLY = $(PLUGIN_NAME).dll

PLUGIN_SOURCES =			\
	$(srcdir)/DPAPBrowser.cs	


REFS =					\
	-r:../../src/f-spot.exe		\
	-r:../../src/FSpot.Core.dll	\
	-r:../../src/FSpot.Widgets.dll \
	-r:../../src/FSpot.Utils.dll	\
	-r:../lib/dpap-sharp.dll	

PKGS =					\
	-pkg:gtk-sharp-2.0

RESOURCES =				\
	-resource:$(srcdir)/$(PLUGIN_MANIFEST)

all: $(PLUGIN_ASSEMBLY)

mpack: $(PLUGIN_ASSEMBLY)
	mautil p $(PLUGIN_ASSEMBLY)

$(PLUGIN_ASSEMBLY): $(PLUGIN_SOURCES) $(PLUGIN_MANIFEST)
	$(CSC_LIB) -out:$@ $(CSC_DEFINES) $(PLUGIN_SOURCES) $(REFS) $(PKGS) $(ASSEMBLIES) $(RESOURCES)

plugindir = $(pkglibdir)/extensions

plugin_DATA =			\
	$(PLUGIN_ASSEMBLY)

EXTRA_DIST = 			\
	$(PLUGIN_SOURCES)	\
	$(PLUGIN_MANIFEST)

CLEANFILES =			\
	$(PLUGIN_ASSEMBLY)	\
	$(PLUGIN_ASSEMBLY).mdb	\
	*.mpack
