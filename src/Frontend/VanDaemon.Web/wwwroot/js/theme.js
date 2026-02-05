// Theme Mode JavaScript Functions for VanDaemon

// Check if browser prefers dark mode
window.vandaemonCheckDarkMode = function() {
    if (window.matchMedia) {
        return window.matchMedia('(prefers-color-scheme: dark)').matches;
    }
    return false;
};

// Set theme (light or dark)
window.vandaemonSetTheme = function(theme) {
    document.documentElement.setAttribute('data-theme', theme);
};

// Start listening for browser dark mode changes
window.vandaemonStartDarkModeListener = function(dotNetReference) {
    if (window.matchMedia) {
        window.vandaemonDarkModeQuery = window.matchMedia('(prefers-color-scheme: dark)');
        window.vandaemonDarkModeHandler = function(e) {
            dotNetReference.invokeMethodAsync('OnBrowserDarkModeChanged', e.matches)
                .catch(err => console.error('Error calling OnBrowserDarkModeChanged:', err));
        };
        window.vandaemonDarkModeQuery.addEventListener('change', window.vandaemonDarkModeHandler);
        console.log('Browser dark mode listener started');
    }
};

// Stop listening for browser dark mode changes
window.vandaemonStopDarkModeListener = function() {
    if (window.vandaemonDarkModeQuery && window.vandaemonDarkModeHandler) {
        window.vandaemonDarkModeQuery.removeEventListener('change', window.vandaemonDarkModeHandler);
        delete window.vandaemonDarkModeQuery;
        delete window.vandaemonDarkModeHandler;
        console.log('Browser dark mode listener stopped');
    }
};
