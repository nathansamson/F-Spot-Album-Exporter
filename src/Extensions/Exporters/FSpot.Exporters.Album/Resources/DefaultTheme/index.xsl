<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
                version="2.0">

	<xsl:param name="createdByText" />
	<xsl:param name="createdByPackage" />
	<xsl:param name="createdByVersion" />

	<xsl:output
		method="xml"
		omit-xml-declaration="yes"
		doctype-system="about:legacy-compat"
		indent="no" />

	<xsl:template match="/">
		<html>
			<head>
				<meta encoding="utf-8" />
				<title><xsl:value-of select="/fspot-album/title" /></title>
				
				<xsl:for-each select="/fspot-album/stylesheets/stylesheet">
					<xsl:choose>
						<xsl:when test="@alternate=alternate">
							<link rel="alternate stylesheet" href="@href" title="@title" />
						</xsl:when>
						<xsl:otherwise>
							<link rel="stylesheet" href="{@href}" />
						</xsl:otherwise>
					</xsl:choose>
				</xsl:for-each>
			</head>
			<body>
				<header>
					<h1><xsl:value-of select="/fspot-album/title" /></h1>
					<p><xsl:value-of select="/fspot-album/description" /></p>
				</header>
				<section class="photolist">
					<xsl:for-each select="/fspot-album/photos/photo">
						<article>
							<a href="img{position()}.html">
								<img src="thumbs/{@filename}" />
							</a>
						</article>
					</xsl:for-each>
				</section>
				<footer>
					<xsl:value-of select="$createdByText" /><xsl:text> </xsl:text>
					<a href="http://f-spot.org">
						<xsl:value-of select="$createdByPackage" /><xsl:text> </xsl:text>
						<xsl:value-of select="$createdByVersion" />
					</a>
				</footer>
			</body>
		</html>
	</xsl:template>
</xsl:stylesheet>