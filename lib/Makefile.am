# Note the list below is sorted by alphabet. Dpap-sharp isn't built.
SUBDIRS = \
	GKeyFile 			\
	gio-sharp			\
	gtk-sharp-beans		\
	Hyena 				\
	libfspot			\
	TagLib 				\
	unique-sharp 		\
	Mono.Google 		\
	Mono.Tabblo 		\
	SmugMugNet


### The stuff below takes care of embedding libraries. Build system hacks! ###
TMPDIR_TAGLIB = /tmp/fetch_taglib

clone-taglib:
	mkdir $(TMPDIR_TAGLIB)
	git clone --depth 1 git://gitorious.org/taglib-sharp/mainline.git $(TMPDIR_TAGLIB)
	pushd $(TMPDIR_TAGLIB); git checkout -b photo-support origin/photo-support; popd


update-taglib:
	rm -rf TagLib/TagLib
	cp -r $(TMPDIR_TAGLIB)/src/TagLib TagLib
	echo "# This file is autogenerated, do not edit!" > TagLib/Makefile.am
	echo "include \$$(top_srcdir)/Makefile.include" >> TagLib/Makefile.am
	echo "ASSEMBLY_NAME = TagLib" >> TagLib/Makefile.am
	echo >> TagLib/Makefile.am
	echo "ASSEMBLY_SOURCES = \\" >> TagLib/Makefile.am
	tail -n+2 TagLib/TagLib/TagLib.sources | sed s/\$$\(srcdir\)\\/// >> TagLib/Makefile.am
	echo >> TagLib/Makefile.am
	echo "ASSEMBLY = \$$(ASSEMBLY_NAME).dll" >> TagLib/Makefile.am
	echo "all: \$$(ASSEMBLY)" >> TagLib/Makefile.am
	echo "\$$(ASSEMBLY): \$$(ASSEMBLY_SOURCES)" >> TagLib/Makefile.am
	echo "	\$$(CSC_LIB) \$$(CSC_DEFINES) -out:\$$@ \$$(ASSEMBLY_SOURCES)" >> TagLib/Makefile.am
	echo >> TagLib/Makefile.am
	echo "assemblydir = \$$(pkglibdir)" >> TagLib/Makefile.am
	echo "assembly_DATA = \$$(ASSEMBLY)" >> TagLib/Makefile.am
	echo "EXTRA_DIST = \$$(ASSEMBLY_SOURCES)" >> TagLib/Makefile.am
	echo "CLEANFILES = \$$(ASSEMBLY) \$$(ASSEMBLY).mdb" >> TagLib/Makefile.am

.PHONY: clone-taglib update-taglib
