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
				<title><xsl:value-of select="/fspot-image/fspot-album/title" /></title>
				
				<xsl:for-each select="/fspot-image/scripts/script">
					<script src="{@src}">
						<xsl:text> </xsl:text>
					</script>
				</xsl:for-each>
				
				<xsl:for-each select="/fspot-image/stylesheets/stylesheet">
					<xsl:choose>
						<xsl:when test="@alternate = 'alternate'">
							<link rel="alternate stylesheet" href="{@href}" title="{@title}" />
						</xsl:when>
						<xsl:otherwise>
							<link rel="stylesheet" href="{@href}">
								<xsl:copy-of select="@title" />
							</link>
						</xsl:otherwise>
					</xsl:choose>
				</xsl:for-each>
			</head>
			<body>
				<header>
					<h1><a href="index.html"><xsl:value-of select="/fspot-image/fspot-album/title" /></a></h1>
					<nav>
						<ol>
							<xsl:if test="/fspot-image/prev">
								<li class="prev"><a href="img{/fspot-image/prev}.html">Previous</a></li>
							</xsl:if>
							<li class="placeholder"></li>
							<xsl:if test="/fspot-image/next">
								<li class="next"><a href="img{/fspot-image/next}.html">Next</a></li>
							</xsl:if>
						</ol>
					</nav>
					<xsl:if test="string-length (/fspot-image/description)">
						<p><xsl:value-of select="/fspot-image/description" /></p>
					</xsl:if>
				</header>
				<section class="photo">
					<a href="hq/{/fspot-image/filename}" target="_blank">
						<img src="mq/{/fspot-image/filename}" />
					</a>
				</section>
				<footer>
					<xsl:value-of select="$createdByText" /><xsl:text> </xsl:text>
					<a href="http://f-spot.org" target="_blank">
						<xsl:value-of select="$createdByPackage" /><xsl:text> </xsl:text>
						<xsl:value-of select="$createdByVersion" />
					</a>
				</footer>
			</body>
		</html>
	</xsl:template>
</xsl:stylesheet>
