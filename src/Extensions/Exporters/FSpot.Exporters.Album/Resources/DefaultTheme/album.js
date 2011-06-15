$(document).ready (function () {
	loadSelectedStylesheet ();
	renderStylesheetList ();
});


function loadSelectedStylesheet () {
	if (localStorage) {
		var preferred_style = localStorage.getItem ("preferred-style");
		if (preferred_style) {
			loadStyle (preferred_style);
		}
	}
}

function loadStyle (style) {
	$('link[rel*="stylesheet"][title]').each(function (i) {
		this.disabled = (this.title != style);
	});
}

function renderStylesheetList () {
	var styles = $('<div id="styleselector">Styles</div>');
	var list = $('<ol></ol>');
	styles.append (list);
	
	$('link[rel*="stylesheet"][title]').each(function (i) {
		var item = $('<li>' + this.title + '</li>');
		item.click (function (style) {
			return function () {
				loadStyle (style);
				localStorage.setItem ("preferred-style", style);
			};
		}(this.title));
		list.append (item);
	});
	
	$('body > footer').append (styles);
}