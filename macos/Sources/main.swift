import AppKit
import SwiftUI
import QuartzCore

final class IslandPanel: NSPanel {
    override var canBecomeKey: Bool { true }
    override var canBecomeMain: Bool { false }
}
final class IslandHostingView: NSHostingView<ScaledIslandRoot> {
    override var mouseDownCanMoveWindow: Bool { true }
}

final class MacApp: NSObject, NSApplicationDelegate, NSWindowDelegate {
    let model = IslandModel()
    let panel = IslandPanel(contentRect: .zero, styleMask: [.borderless, .nonactivatingPanel], backing: .buffered, defer: false)
    var status: NSStatusItem?
    var timer: Timer?
    var globalClick: Any?, localClick: Any?, globalUp: Any?, localUp: Any?, localKey: Any?
    var changingFrame = false
    var frameRevision = 0
    var lastInside = Date()
    var floatingFrame: NSRect?
    var hidden = false

    func applicationDidFinishLaunching(_ notification: Notification) {
        panel.title = "WinKnotch"
        panel.isOpaque = false; panel.backgroundColor = .clear; panel.hasShadow = false
        panel.level = .statusBar; panel.hidesOnDeactivate = false
        panel.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary, .stationary]
        panel.delegate = self
        panel.contentView = IslandHostingView(rootView: ScaledIslandRoot(model: model))
        model.resize = { [weak self] in self?.position(animated: true) }
        model.recover = { [weak self] in self?.findIsland() }
        menuBar()
        position(animated: false); panel.orderFrontRegardless(); model.start()
        timer = Timer(timeInterval: 0.1, repeats: true) { [weak self] _ in self?.tick() }
        RunLoop.main.add(timer!, forMode: .common)
        NotificationCenter.default.addObserver(self, selector: #selector(screensChanged), name: NSApplication.didChangeScreenParametersNotification, object: nil)
        globalClick = NSEvent.addGlobalMonitorForEvents(matching: [.leftMouseDown, .rightMouseDown]) { [weak self] _ in self?.outsideClick() }
        localClick = NSEvent.addLocalMonitorForEvents(matching: [.leftMouseDown, .rightMouseDown]) { [weak self] event in self?.outsideClick(); return event }
        globalUp = NSEvent.addGlobalMonitorForEvents(matching: .leftMouseUp) { [weak self] _ in self?.endPotentialDrag() }
        localUp = NSEvent.addLocalMonitorForEvents(matching: .leftMouseUp) { [weak self] event in self?.endPotentialDrag(); return event }
        localKey = NSEvent.addLocalMonitorForEvents(matching: .keyDown) { [weak self] event in
            if event.keyCode == 53 { self?.model.cancelAppDrag(); self?.model.setExpanded(false) }
            return event
        }
        if CommandLine.arguments.contains("--smoke-test") {
            DispatchQueue.main.asyncAfter(deadline: .now() + 4) { print("AppKit launch smoke test passed"); NSApp.terminate(nil) }
        }
        if CommandLine.arguments.contains("--render-previews") { renderPreviews(index: 0) }
    }

    func menuBar() {
        status = NSStatusBar.system.statusItem(withLength: NSStatusItem.squareLength)
        status?.button?.image = NSImage(systemSymbolName: "waveform", accessibilityDescription: "WinKnotch")
        let menu = NSMenu()
        func item(_ name: String, _ selector: Selector, key: String = "") { let item = menu.addItem(withTitle: name, action: selector, keyEquivalent: key); item.target = self }
        item("Find & re-center island", #selector(findIsland))
        item("Settings…", #selector(settings), key: ",")
        menu.addItem(.separator())
        item("Connect Apple Music", #selector(connectMusic)); item("Connect Spotify", #selector(connectSpotify))
        let screens = NSMenu()
        for (index, screen) in NSScreen.screens.enumerated() { let item = screens.addItem(withTitle: screen.localizedName, action: #selector(selectScreen(_:)), keyEquivalent: ""); item.tag = index; item.target = self }
        let move = menu.addItem(withTitle: "Move to display", action: nil, keyEquivalent: ""); move.submenu = screens
        menu.addItem(.separator())
        menu.addItem(withTitle: "Quit WinKnotch", action: #selector(NSApplication.terminate(_:)), keyEquivalent: "q")
        status?.menu = menu
    }
    var screen: NSScreen? {
        let screens = NSScreen.screens
        return screens.indices.contains(model.screenIndex) ? screens[model.screenIndex] : NSScreen.main
    }
    func expectedFrame() -> NSRect {
        let bounds = screen?.frame ?? NSRect(x: 0, y: 0, width: 1280, height: 800)
        let width = min(bounds.width - 20, (model.expanded ? model.pageWidth : model.compactWidth) * model.scale)
        let safeTop = screen?.safeAreaInsets.top ?? 0
        let closedHeight = max(38 * model.scale, safeTop)
        let height = model.expanded ? min(bounds.height - 70, (model.pageHeight + (model.tabsOpen ? 35 : 0)) * model.scale) : closedHeight
        let gap: CGFloat = model.expanded ? 12 : 0
        if model.placement == "Floating", let saved = floatingFrame {
            return NSRect(x: min(max(bounds.minX, saved.midX - width / 2), bounds.maxX - width), y: min(max(bounds.minY, saved.maxY - height), bounds.maxY - height), width: width, height: height)
        }
        let y = model.placement == "Bottom" ? bounds.minY + gap : hidden ? bounds.maxY - 4 : bounds.maxY - height - gap
        return NSRect(x: round(bounds.midX - width / 2), y: round(y), width: round(width), height: round(height))
    }
    func position(animated: Bool) {
        panel.isMovableByWindowBackground = model.placement == "Floating"
        frameRevision += 1; let revision = frameRevision
        changingFrame = true
        let frame = expectedFrame()
        if animated {
            NSAnimationContext.runAnimationGroup { context in
                context.duration = model.expanded ? 0.46 : 0.36
                context.timingFunction = CAMediaTimingFunction(controlPoints: 0.22, 0.05, 0.18, 1)
                panel.animator().setFrame(frame, display: true)
            } completionHandler: { [weak self] in if self?.frameRevision == revision { self?.changingFrame = false } }
        } else { panel.setFrame(frame, display: true); changingFrame = false }
    }
    func tick() {
        if CommandLine.arguments.contains("--render-previews") { return }
        let mouse = NSEvent.mouseLocation
        let inside = panel.frame.insetBy(dx: -5, dy: -3).contains(mouse)
        if inside { lastInside = Date() }
        if model.expanded && !inside && !model.tabsOpen && model.page != .settings && !model.seeking && model.draggedApp == nil && NSApp.modalWindow == nil && Date().timeIntervalSince(lastInside) > 0.65 {
            model.setExpanded(false)
        }
        let bounds = screen?.frame ?? .zero
        let nearEdge = abs(mouse.x - bounds.midX) < model.compactWidth * model.scale / 2 + 20
            && (model.placement == "Bottom" ? mouse.y < bounds.minY + 7 : mouse.y > bounds.maxY - 7)
        let shouldHide = model.autoHide && model.placement == "Top" && !model.expanded && !inside && !nearEdge && model.ringing == nil && Date().timeIntervalSince(lastInside) > 3
        if shouldHide != hidden { hidden = shouldHide; position(animated: true) }
        if !changingFrame && model.placement != "Floating" {
            let expected = expectedFrame()
            if abs(panel.frame.width - expected.width) > 1 || abs(panel.frame.height - expected.height) > 1 { position(animated: true) }
        }
    }
    func outsideClick() { if NSApp.modalWindow == nil && !panel.frame.contains(NSEvent.mouseLocation) { model.setExpanded(false) } }
    func endPotentialDrag() { DispatchQueue.main.asyncAfter(deadline: .now() + 0.2) { [weak self] in self?.model.cancelAppDrag() } }
    func windowDidMove(_ notification: Notification) { if !changingFrame && model.placement == "Floating" { floatingFrame = panel.frame } }
    @objc func screensChanged() { model.screenIndex = min(model.screenIndex, max(0, NSScreen.screens.count - 1)); menuBar(); position(animated: false) }
    @objc func selectScreen(_ item: NSMenuItem) { model.screenIndex = item.tag; floatingFrame = nil; position(animated: true) }
    @objc func findIsland() {
        model.placement = "Top"; floatingFrame = nil; hidden = false; lastInside = Date(); model.save()
        position(animated: true); panel.orderFrontRegardless(); model.flash = true
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.1) { withAnimation(.easeOut(duration: 0.6)) { self.model.flash = false } }
    }
    @objc func settings() { model.setExpanded(true); model.select(.settings); panel.makeKeyAndOrderFront(nil) }
    @objc func connectMusic() { model.connect(Players.music) }
    @objc func connectSpotify() { model.connect(Players.spotify) }
    func renderPreviews(index: Int) {
        let names = ["media", "settings-light", "settings-custom", "clock", "timer", "apps"]
        guard index < names.count else { print("Native previews rendered"); NSApp.terminate(nil); return }
        model.expanded = true; model.tabsOpen = false; model.theme = "Dark"; model.scale = 1; model.opacity = 1
        model.track = nil; model.artwork = nil
        switch index {
        case 0:
            model.page = .media
            model.track = PlayingTrack(app: Players.music, title: "WinKnotch for Mac", artist: "Native media island preview", playing: false, position: 65, duration: 240, artworkURL: "")
            model.position = 65
            let image = NSImage(size: NSSize(width: 256, height: 256))
            image.lockFocus()
            NSGradient(starting: NSColor(srgbRed: 0.20, green: 0.48, blue: 0.88, alpha: 1), ending: NSColor(srgbRed: 0.63, green: 0.28, blue: 0.74, alpha: 1))?.draw(in: NSRect(x: 0, y: 0, width: 256, height: 256), angle: 35)
            image.unlockFocus(); model.artwork = image
        case 1: model.page = .settings; model.theme = "Light"
        case 2: model.page = .settings; model.theme = "Custom"; model.customColor = Color(NSColor(hex: "#28959A")!); model.opacity = 0.75
        case 3: model.page = .clock; model.tool = .clock
        case 4: model.page = .clock; model.tool = .timer; model.countdownEnd = Date().addingTimeInterval(120)
        default: model.page = .apps; model.appLayout = "Dock"; model.editingApps = false; model.apps = ["/System/Applications/Calculator.app", "/System/Applications/Notes.app", "/System/Applications/Calendar.app"]
        }
        position(animated: false)
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.7) {
            if index == 2 && self.model.tint.hex != "#28959A" {
                FileHandle.standardError.write(Data("Color picker changed the selected color while rendering\n".utf8)); exit(1)
            }
            let folder = URL(fileURLWithPath: FileManager.default.currentDirectoryPath).appendingPathComponent("macos/build/previews")
            do {
                try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
                if let view = self.panel.contentView, let bitmap = view.bitmapImageRepForCachingDisplay(in: view.bounds) {
                    view.cacheDisplay(in: view.bounds, to: bitmap)
                    if let data = bitmap.representation(using: .png, properties: [:]) { try data.write(to: folder.appendingPathComponent(names[index] + ".png")) }
                }
            } catch { FileHandle.standardError.write(Data("Preview export failed: \(error)\n".utf8)) }
            self.renderPreviews(index: index + 1)
        }
    }
    func applicationWillTerminate(_ notification: Notification) {
        timer?.invalidate(); model.dismiss()
        for monitor in [globalClick, localClick, globalUp, localUp, localKey] { if let monitor = monitor { NSEvent.removeMonitor(monitor) } }
    }
}

func selfTest() {
    func check(_ condition: Bool, _ name: String) {
        guard condition else { FileHandle.standardError.write(Data("FAILED: \(name)\n".utf8)); exit(1) }
        print("PASS: \(name)")
    }
    check(IslandModel.duration(3661) == "01:01:01", "Duration formatting")
    check(IslandModel.duration(-1) == "00:00:00", "Negative duration")
    check(NSColor(hex: "#28959A")?.hex == "#28959A", "Hex color round trip")
    check(NSColor(hex: "invalid") == nil, "Invalid hex color")
    let teal = NSColor(hex: "#28959A")!.usingColorSpace(.sRGB)!
    check(NSColor.hsv(teal.hueComponent, saturation: teal.saturationComponent, brightness: teal.brightnessComponent).hex == "#28959A", "Color block HSV round trip")
    let model = IslandModel()
    model.theme = "Custom"; model.customColor = Color(NSColor(hex: "#28959A")!); model.opacity = 0.53
    check(!model.lightSurface, "Transparent teal uses light text")
    model.customColor = Color(NSColor(hex: "#D4EDFC")!); model.opacity = 1
    check(model.lightSurface, "Opaque pale color uses dark text")
    model.timerHours = "0"; model.timerMinutes = "0"; model.timerSeconds = "2"; model.startTimer()
    check((model.countdownEnd?.timeIntervalSinceNow ?? 0) > 1.5 && (model.countdownEnd?.timeIntervalSinceNow ?? 0) <= 2, "Two-second timer")
    model.countdownEnd = nil; model.timerSeconds = "0"; model.startTimer(); check(model.countdownEnd == nil, "Zero timer rejected")
    model.apps = ["A", "B", "C"]; model.originalApps = model.apps; model.draggedApp = "A"; model.apps = ["B", "A", "C"]; model.cancelAppDrag()
    check(model.apps == ["A", "B", "C"], "Canceled app reordering")
    let result = Players.script("return {\"title\", \"artist\", true, 12, 180, \"\"}").0
    check(result?.numberOfItems == 6 && result?.atIndex(4)?.int32Value == 12, "AppleScript result parsing")
    print("Duration, color contrast, two-second timer, canceled dock reorder, and AppleScript result checks passed")
}

let application = NSApplication.shared
application.setActivationPolicy(.accessory)
if CommandLine.arguments.contains("--self-test") { selfTest() }
else {
    let delegate = MacApp()
    application.delegate = delegate
    withExtendedLifetime(delegate) { application.run() }
}
