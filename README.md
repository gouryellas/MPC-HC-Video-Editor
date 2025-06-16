# mpcbe-video-editor
Fast way to cut and merge video clips using mpc-be player.

## Configuration

The main bookmark hotkeys are configurable via `data/settings.ini`:

```
[Hotkeys]
main=MButton
erase=+space
```

Change `main` or `erase` to any valid AutoHotkey hotkey name to override the
default middle mouse and Shift+Space bindings.

The `erase` hotkey is essentially an **undo** for bookmarks—it removes the last
timestamp that was saved.
