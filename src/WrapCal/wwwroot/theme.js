// The theme preference lives in cookies, and this script is their only owner: an interactive
// component cannot write a response cookie, and there is no request for one to read.
window.wrapcalTheme = {
    // Asked for on the layout's first render, so the page is drawn in the right theme from the
    // start rather than flashing light and correcting a frame later. An unset cookie is null,
    // which is what a first visit looks like.
    load: function () {
        return {
            choice: read("wrapcal-theme"),
            system: read("wrapcal-system-theme")
        };
    },

    set: function (choice) {
        write("wrapcal-theme", choice);
    },

    // Remembered even when the choice is explicit: it is what the next page load is drawn with,
    // so a reader in System mode does not see the page flash the wrong way round first.
    rememberSystem: function (isDark) {
        write("wrapcal-system-theme", isDark ? "dark" : "light");
    }
};

function read(name) {
    const match = document.cookie.match(new RegExp("(^|;\\s*)" + name + "=([^;]*)"));
    return match ? decodeURIComponent(match[2]) : null;
}

function write(name, value) {
    document.cookie = name + "=" + value + ";path=/;max-age=31536000;samesite=lax";
}
