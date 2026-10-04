// VanDaemon native-bridge transport shim (feature 008-native-bridge-transport).
//
// Owns the `window.VanDaemonNativeBridge` @JavascriptInterface member names in ONE place and adapts
// the Kotlin shell's synchronous native returns for the WASM/C# `JsInteropNativeBridge`
// (the C# half of the 004 INativeBridge contract; wire shape: specs/005-launcher-shell/
// contracts/js-interop-bridge.md).
//
// Fail-safe by design (contract G5): when the native object is absent — desktop browser, off-device,
// or before injection/readiness — every call returns the contract's defined stub default and nothing
// throws. On a real unit the Kotlin shell has injected `window.VanDaemonNativeBridge`, so these
// forward across the WebView.
(function () {
    "use strict";

    function native() {
        return (typeof window !== "undefined") ? window.VanDaemonNativeBridge : undefined;
    }

    window.vandaemonBridge = {
        // Synchronous presence probe used by NativeBridgeFactory (IJSInProcessRuntime.Invoke<bool>).
        hasNative: function () {
            var n = native();
            return typeof n !== "undefined" && n !== null;
        },

        // UI -> native (request/response). Return the native value, or the stub default if absent.
        getReversingState: function () {
            var n = native();
            return (n && typeof n.getReversingState === "function") ? n.getReversingState() : false;
        },

        getAccState: function () {
            var n = native();
            return (n && typeof n.getAccState === "function") ? n.getAccState() : "Unknown";
        },

        openDsp: function () {
            var n = native();
            if (n && typeof n.openDsp === "function") {
                n.openDsp();
            }
            // Absent native -> no-op resolve (matches the stub).
        },

        // native -> UI (push). Install the page-side hook the shell calls via evaluateJavascript,
        // marshalling `{ key, timestampUtc }` back into .NET via the supplied DotNetObjectReference.
        registerWheelKey: function (dotNetRef) {
            window.VanDaemonBridgeEvents = window.VanDaemonBridgeEvents || {};
            window.VanDaemonBridgeEvents.onWheelKey = function (e) {
                try {
                    var key = (e && e.key) ? e.key : "Unknown";
                    var ts = (e && e.timestampUtc) ? e.timestampUtc : "";
                    dotNetRef.invokeMethodAsync("OnWheelKey", key, ts);
                } catch (err) {
                    console.error("vandaemonBridge: wheel-key dispatch to .NET failed:", err);
                }
            };
        },

        unregisterWheelKey: function () {
            if (window.VanDaemonBridgeEvents) {
                window.VanDaemonBridgeEvents.onWheelKey = undefined;
            }
        }
    };
})();
