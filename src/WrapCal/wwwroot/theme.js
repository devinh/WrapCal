window.wrapcalTheme = {
    set: function (isDark) {
        document.cookie = "wrapcal-theme=" + (isDark ? "dark" : "light")
            + ";path=/;max-age=31536000;samesite=lax";
    }
};
