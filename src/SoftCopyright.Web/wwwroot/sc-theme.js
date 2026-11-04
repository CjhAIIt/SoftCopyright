// 深浅主题：首屏在样式加载前套用，避免闪烁；切换结果写入 localStorage。
window.scTheme = (function () {
    const KEY = 'sc-theme';

    function current() {
        return document.documentElement.getAttribute('data-theme') === 'dark';
    }

    function apply(isDark) {
        document.documentElement.setAttribute('data-theme', isDark ? 'dark' : 'light');
        return isDark;
    }

    return {
        isDark: function () {
            return current();
        },
        toggle: function () {
            const next = !current();
            apply(next);
            try {
                localStorage.setItem(KEY, next ? 'dark' : 'light');
            } catch (e) {
                // 隐私模式下 localStorage 可能不可用，忽略即可。
            }
            return next;
        }
    };
})();
