# VanDaemon PWA Setup

VanDaemon is now configured as a Progressive Web App (PWA), enabling installation on devices and offline functionality.

## PWA Features

### ✅ Already Configured
- **Web Manifest** (`manifest.json`) - App metadata, icons, and display settings
- **Service Worker** (`service-worker.js`) - Offline support and caching
- **App Icons** - Favicon.svg for all sizes
- **iOS Support** - Apple-specific meta tags for iOS home screen
- **Offline Caching** - Static assets cached for offline use
- **Install Prompts** - Browser will prompt to install app

### 📱 Installation

#### Desktop (Chrome, Edge, Brave)
1. Visit the VanDaemon web app
2. Look for install icon in address bar (or ⋮ menu → "Install VanDaemon")
3. Click "Install"
4. App opens in standalone window

#### Android
1. Visit the VanDaemon web app in Chrome
2. Tap the menu (⋮) → "Add to Home screen" or "Install app"
3. Confirm installation
4. App icon appears on home screen

#### iOS (Safari)
1. Visit the VanDaemon web app in Safari
2. Tap the Share button (□↑)
3. Scroll down and tap "Add to Home Screen"
4. Tap "Add"
5. App icon appears on home screen

## Icon Generation

The app includes an icon generator utility to create PNG icons from the SVG favicon.

### Generate Icons
1. Navigate to: `http://your-vandaemon-url/generate-icons.html`
2. Click "Generate Icons" button
3. Download the generated PNG files:
   - `icon-192.png` (192x192)
   - `icon-512.png` (512x512)
4. Save them to `/wwwroot/` directory

**Note:** Until PNG icons are generated, the SVG favicon will be used for all sizes.

## Service Worker Caching Strategy

### Cache-First for Static Assets
- HTML files
- CSS stylesheets
- JavaScript bundles
- Images and icons
- Fonts

### Network-First for Dynamic Data
- API calls (`/api/*`)
- SignalR hubs (`/hubs/*`)
- Real-time telemetry data

### Offline Behavior
- Static pages available offline
- Cached van diagram visible offline
- Real-time data requires connection
- Graceful degradation when offline

## Testing PWA Features

### Chrome DevTools
1. Open DevTools (F12)
2. Go to **Application** tab
3. Check:
   - **Manifest** - View manifest.json
   - **Service Workers** - View worker status
   - **Cache Storage** - View cached files
   - **Lighthouse** - Run PWA audit

### Lighthouse PWA Audit
1. Open DevTools (F12)
2. Go to **Lighthouse** tab
3. Select "Progressive Web App"
4. Click "Generate report"
5. Review PWA score and recommendations

## Manifest Configuration

Located at `/wwwroot/manifest.json`:

```json
{
  "name": "VanDaemon Control System",
  "short_name": "VanDaemon",
  "display": "standalone",
  "start_url": "/",
  "theme_color": "#2196F3",
  "background_color": "#2196F3"
}
```

### Display Modes
- **standalone**: App opens without browser UI (current setting)
- **fullscreen**: Full screen mode (optional for camper van use)
- **minimal-ui**: Minimal browser UI
- **browser**: Normal browser tab

To change display mode, edit `display` property in `manifest.json`.

## Updating the Service Worker

When you update the service worker:

1. Increment `CACHE_NAME` version in `service-worker.js`:
   ```javascript
   const CACHE_NAME = 'vandaemon-v2'; // Increment version
   ```

2. The browser will detect the new worker
3. Console will log: "New service worker available. Refresh to update."
4. Users should refresh to get the latest version

## Shortcuts

The manifest includes app shortcuts for quick access:
- **Dashboard** - View van diagram
- **Tanks** - Monitor tank levels
- **Controls** - Control lights and systems

These appear in the right-click context menu on installed app icon (desktop) or long-press (mobile).

## Troubleshooting

### Service Worker Not Registering
- Check browser console for errors
- Ensure served over HTTPS (or localhost)
- Clear browser cache and reload

### Icons Not Showing
1. Generate PNG icons using `generate-icons.html`
2. Verify files exist in `/wwwroot/`
3. Clear browser cache
4. Reinstall app

### Updates Not Appearing
1. Increment service worker version
2. Clear browser cache
3. Unregister old service worker in DevTools
4. Hard refresh (Ctrl+Shift+R)

## Production Deployment

For production deployment:

1. **Generate PNG icons** using the icon generator
2. **Add screenshot** for app stores: `/wwwroot/screenshot-dashboard.png`
3. **Test offline functionality** with DevTools offline mode
4. **Run Lighthouse audit** to ensure PWA compliance
5. **Configure HTTPS** - PWAs require secure context
6. **Add offline page** (optional): `/wwwroot/offline.html`

## Additional Resources

- [MDN: Progressive Web Apps](https://developer.mozilla.org/en-US/docs/Web/Progressive_web_apps)
- [web.dev: PWA](https://web.dev/progressive-web-apps/)
- [PWA Builder](https://www.pwabuilder.com/)
