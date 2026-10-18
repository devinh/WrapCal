// An interactive component cannot write a response cookie, so the theme preference is written
// from here — whether the reader chose it, or the browser observed the system change.
window.wrapcalTheme = {
    set: function (choice) {
        write("wrapcal-theme", choice);
    },

    // Remembered even when the choice is explicit: it is what the next page load is drawn with,
    // so a reader in System mode does not see the page flash the wrong way round first.
    rememberSystem: function (isDark) {
        write("wrapcal-system-theme", isDark ? "dark" : "light");
    }
};

function write(name, value) {
    document.cookie = name + "=" + value + ";path=/;max-age=31536000;samesite=lax";
}
