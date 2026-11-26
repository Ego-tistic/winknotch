import AppKit
import SwiftUI
import ServiceManagement

enum IslandPage: String, CaseIterable { case media = "Media", clock = "Clock", apps = "Apps", settings = "Settings" }
enum ClockTool: String, CaseIterable { case clock = "Clock", stopwatch = "Stopwatch", timer = "Timer", wake = "Wake-up" }

final class IslandModel: ObservableObject {
    @Published var page: IslandPage = .clock
    @Published var expanded = false
    @Published var tabsOpen = false
    @Published var hovering = false
    @Published var flash = false
    @Published var track: PlayingTrack?
    @Published var artwork: NSImage?
    @Published var accent = NSColor.white
    @Published var now = Date()
    @Published var position = 0.0
    @Published var seeking = false
    @Published var volume = 50.0
    @Published var tool: ClockTool = .clock
    @Published var stopwatchStart: Date?
    @Published var stopwatchElapsed = 0.0
    @Published var timerHours = "0"
    @Published var timerMinutes = "0"
    @Published var timerSeconds = "0"
    @Published var wakeHours = "7"
    @Published var wakeMinutes = "0"
    @Published var wakeSeconds = "0"
    @Published var countdownEnd: Date?
    @Published var wakeDate: Date?
    @Published var ringing: String?
    @Published var notice = ""
    @Published var editingApps = false
    @Published var search = ""
    @Published var draggedApp: String?
    var originalApps: [String] = []
    @Published var apps: [String]
    @Published var theme: String
    @Published var customColor: Color
    @Published var opacity: Double
    @Published var scale: Double
    @Published var militaryTime: Bool
    @Published var autoHide: Bool
    @Published var placement: String
    @Published var appLayout: String
    @Published var showAppNames: Bool
    @Published var selectedSound: String
    @Published var enabledPlayers: Set<String>
    @Published var playerError = ""
    @Published var screenIndex = 0
    @Published var loginEnabled = false
    var resize: (() -> Void)?
    var recover: (() -> Void)?
    private let queue = DispatchQueue(label: "io.winknotch.media", qos: .utility)
    private var polling = false
    private var nextPoll = Date.distantPast
    private var observed = Date()
    private var trackPosition = 0.0
    private var previouslyPlaying = Set<String>()
    private var pausedSince = Date()
    private var artKey = ""
    private var ringStarted = Date.distantPast
    private var sound: NSSound?
    private var ticker: Timer?

    init() {
        let d = UserDefaults.standard
        apps = d.stringArray(forKey: "apps") ?? []
        theme = d.string(forKey: "theme") ?? "Dark"
        customColor = Color(NSColor(hex: d.string(forKey: "customColor") ?? "#526D9A") ?? .systemBlue)
        opacity = d.object(forKey: "opacity") as? Double ?? 0.92
        scale = d.object(forKey: "scale") as? Double ?? 1
        militaryTime = d.bool(forKey: "militaryTime")
        autoHide = d.bool(forKey: "autoHide")
        placement = d.string(forKey: "placement") ?? "Top"
        appLayout = d.string(forKey: "appLayout") ?? "Dock"
        showAppNames = d.object(forKey: "showAppNames") as? Bool ?? true
        selectedSound = d.string(forKey: "sound") ?? "Glass"
        enabledPlayers = Set(d.stringArray(forKey: "players") ?? [])
        opacity = min(1, max(0, opacity)); scale = min(1.5, max(0.6, scale))
    }

    var tint: NSColor { theme == "Custom" ? NSColor(customColor) : theme == "Light" ? NSColor(calibratedWhite: 0.96, alpha: 1) : NSColor(calibratedWhite: 0.045, alpha: 1) }
    var lightSurface: Bool {
        if theme != "Custom" { return theme == "Light" }
        guard let c = tint.usingColorSpace(.sRGB) else { return false }
        func linear(_ v: CGFloat) -> Double { let x = Double(v) * opacity * opacity; return x <= 0.04045 ? x / 12.92 : pow((x + 0.055) / 1.055, 2.4) }
        return 0.2126 * linear(c.redComponent) + 0.7152 * linear(c.greenComponent) + 0.0722 * linear(c.blueComponent) > 0.179
    }
    var ink: Color { lightSurface ? Color(white: 0.07) : Color(white: 0.97) }
    var secondary: Color { ink.opacity(0.75) }
    var compactClock: Bool { track?.playing != true && (page == .clock || now.timeIntervalSince(pausedSince) > 60 || track == nil) }
    var clockText: String { let formatter = DateFormatter(); formatter.dateFormat = militaryTime ? "HH:mm:ss" : "h:mm:ss a"; return formatter.string(from: now) }
    var countdown: Double { max(0, countdownEnd?.timeIntervalSince(now) ?? 0) }
    var stopwatchValue: Double { stopwatchElapsed + (stopwatchStart.map { now.timeIntervalSince($0) } ?? 0) }
    var selectedApps: [String] { apps.filter { search.isEmpty || appName($0).localizedCaseInsensitiveContains(search) } }
    var pageHeight: CGFloat {
        switch page {
        case .media: return 270
        case .clock: return tool == .clock ? 186 : 280
        case .apps: return appLayout == "Dock" && !editingApps ? 192 : 330
        case .settings: return 470
        }
    }
    var pageWidth: CGFloat { page == .apps && appLayout == "Dock" && !editingApps ? 420 : page == .clock && tool == .clock ? 340 : 360 }
    var compactWidth: CGFloat { ringing != nil ? 244 : countdownEnd != nil ? 196 : compactClock ? 206 : 190 }

    func start() {
        ticker = Timer(timeInterval: 0.1, repeats: true) { [weak self] _ in self?.tick() }
        RunLoop.main.add(ticker!, forMode: .common)
        if #available(macOS 13, *) { loginEnabled = SMAppService.mainApp.status == .enabled }
        queue.async {
            let value = Players.script("output volume of (get volume settings)").0?.int32Value ?? 50
            DispatchQueue.main.async { self.volume = Double(value) }
        }
    }
    func save() {
        if CommandLine.arguments.contains("--render-previews") || CommandLine.arguments.contains("--self-test") { return }
        let d = UserDefaults.standard
        d.set(apps, forKey: "apps"); d.set(theme, forKey: "theme")
        d.set(NSColor(customColor).hex, forKey: "customColor"); d.set(opacity, forKey: "opacity")
        d.set(scale, forKey: "scale"); d.set(militaryTime, forKey: "militaryTime")
        d.set(autoHide, forKey: "autoHide"); d.set(placement, forKey: "placement")
        d.set(appLayout, forKey: "appLayout"); d.set(showAppNames, forKey: "showAppNames")
        d.set(selectedSound, forKey: "sound"); d.set(Array(enabledPlayers), forKey: "players")
    }
    func setExpanded(_ value: Bool) {
        guard value != expanded else { return }
        if value && track?.playing == true { page = .media }
        withAnimation(.easeInOut(duration: 0.38)) { expanded = value }
        if !value { tabsOpen = false; editingApps = false; if page == .settings { page = track?.playing == true ? .media : .clock } }
        resize?()
    }
    func select(_ value: IslandPage) { page = value; expanded = true; resize?() }
    private func tick() {
        now = Date()
        if !seeking, let track = track { position = min(track.duration, trackPosition + (track.playing ? now.timeIntervalSince(observed) : 0)) }
        if let end = countdownEnd, now >= end { countdownEnd = nil; ring("Timer finished") }
        if let end = wakeDate, now >= end { wakeDate = nil; ring("Wake-up alarm") }
        if ringing != nil && now.timeIntervalSince(ringStarted) >= 180 { dismiss() }
        if CommandLine.arguments.contains("--render-previews") { return }
        guard !polling, now >= nextPoll else { return }
        nextPoll = now.addingTimeInterval(1)
        let active = Players.all.filter { enabledPlayers.contains($0) && !NSRunningApplication.runningApplications(withBundleIdentifier: $0).isEmpty }
        guard !active.isEmpty else { if track != nil { track = nil; artwork = nil }; return }
        polling = true
        let previous = previouslyPlaying, old = track?.app
        queue.async {
            var result: [PlayingTrack] = [], errors: [String] = []
            for id in active { let (track, error) = Players.read(id); if let track = track { result.append(track) }; if let error = error { errors.append(error) } }
            let playing = result.filter { $0.playing }
            let chosen = playing.first { !previous.contains($0.app) } ?? playing.first { $0.app == old } ?? playing.first ?? result.first { $0.app == old } ?? result.first
            DispatchQueue.main.async {
                self.polling = false; self.previouslyPlaying = Set(playing.map { $0.app })
                self.playerError = errors.first ?? ""
                if self.track?.playing == true && chosen?.playing != true { self.pausedSince = Date() }
                self.track = chosen; self.trackPosition = chosen?.position ?? 0; self.observed = Date()
                if let chosen = chosen, self.artKey != chosen.key {
                    self.artKey = chosen.key; self.artwork = nil; self.accent = .white; self.fetchArtwork(chosen)
                }
            }
        }
    }
    private func fetchArtwork(_ track: PlayingTrack) {
        if track.app == Players.music {
            queue.async { let image = Players.artwork(track.app); DispatchQueue.main.async { self.acceptArt(image, key: track.key) } }
        } else if let url = URL(string: track.artworkURL), url.scheme == "https" || url.scheme == "http" {
            URLSession.shared.dataTask(with: url) { data, _, _ in
                let image = data.flatMap { NSImage(data: $0) }
                DispatchQueue.main.async { self.acceptArt(image, key: track.key) }
            }.resume()
        }
    }
    private func acceptArt(_ image: NSImage?, key: String) {
        guard key == track?.key else { return }
        artwork = image
        if let data = image?.tiffRepresentation, let bitmap = NSBitmapImageRep(data: data), bitmap.pixelsWide > 0, bitmap.pixelsHigh > 0 {
            var r: CGFloat = 0, g: CGFloat = 0, b: CGFloat = 0, count: CGFloat = 0
            for y in stride(from: 0, to: bitmap.pixelsHigh, by: max(1, bitmap.pixelsHigh / 16)) {
                for x in stride(from: 0, to: bitmap.pixelsWide, by: max(1, bitmap.pixelsWide / 16)) {
                    guard let color = bitmap.colorAt(x: x, y: y)?.usingColorSpace(.sRGB), color.alphaComponent > 0.5 else { continue }
                    r += color.redComponent; g += color.greenComponent; b += color.blueComponent; count += 1
                }
            }
            if count > 0 { accent = NSColor(calibratedRed: r / count, green: g / count, blue: b / count, alpha: 1) }
        }
    }
    func connect(_ id: String) {
        enabledPlayers.insert(id); playerError = ""; save()
        if let url = NSWorkspace.shared.urlForApplication(withBundleIdentifier: id) {
            NSWorkspace.shared.openApplication(at: url, configuration: NSWorkspace.OpenConfiguration()) { _, _ in DispatchQueue.main.async { self.nextPoll = .distantPast } }
        } else { playerError = "Install \(Players.name(id)), then connect it here." }
    }
    func command(_ body: String) {
        guard let app = track?.app else { return }
        queue.async {
            let (_, error) = Players.script(body, app: app)
            DispatchQueue.main.async { self.playerError = error ?? ""; self.nextPoll = .distantPast }
        }
    }
    func seek() { command("set player position to \(max(0, position))") }
    func setVolume() { let value = Int(volume.rounded()); queue.async { _ = Players.script("set volume output volume \(value)") } }
    func openPlayer() {
        guard let id = track?.app, let app = NSRunningApplication.runningApplications(withBundleIdentifier: id).first else { return }
        app.activate(options: [.activateAllWindows, .activateIgnoringOtherApps]); setExpanded(false)
    }
    func toggleStopwatch() {
        if let start = stopwatchStart { stopwatchElapsed += now.timeIntervalSince(start); stopwatchStart = nil }
        else { stopwatchStart = now }
    }
    func startTimer() {
        let hours = min(99, max(0, Int(timerHours) ?? 0)), minutes = min(59, max(0, Int(timerMinutes) ?? 0)), seconds = min(59, max(0, Int(timerSeconds) ?? 0))
        let total = hours * 3600 + minutes * 60 + seconds
        guard total > 0 else { notice = "Choose a duration greater than zero."; return }
        countdownEnd = Date().addingTimeInterval(Double(total)); notice = ""; dismiss()
    }
    func setWake() {
        let components = DateComponents(hour: min(23, max(0, Int(wakeHours) ?? 0)), minute: min(59, max(0, Int(wakeMinutes) ?? 0)), second: min(59, max(0, Int(wakeSeconds) ?? 0)))
        wakeDate = Calendar.current.nextDate(after: Date(), matching: components, matchingPolicy: .nextTime)
        notice = ""; dismiss()
    }
    private func ring(_ name: String) {
        ringing = name; ringStarted = Date(); sound?.stop(); sound = NSSound(named: NSSound.Name(selectedSound)); sound?.loops = true; sound?.play()
        page = .clock; resize?()
    }
    func previewSound() { NSSound(named: NSSound.Name(selectedSound))?.play() }
    func dismiss() { sound?.stop(); sound = nil; ringing = nil }
    func addApps() {
        let panel = NSOpenPanel(); panel.directoryURL = URL(fileURLWithPath: "/Applications")
        panel.canChooseFiles = true; panel.canChooseDirectories = false; panel.allowsMultipleSelection = true
        panel.allowedFileTypes = ["app"]; panel.treatsFilePackagesAsDirectories = false
        panel.prompt = "Add to island"
        if panel.runModal() == .OK { for url in panel.urls where !apps.contains(url.path) { apps.append(url.path) }; save() }
    }
    func appName(_ path: String) -> String { FileManager.default.displayName(atPath: path).replacingOccurrences(of: ".app", with: "") }
    func openApp(_ path: String) {
        if let id = Bundle(path: path)?.bundleIdentifier, let app = NSRunningApplication.runningApplications(withBundleIdentifier: id).first {
            app.activate(options: [.activateAllWindows, .activateIgnoringOtherApps])
        } else {
            let config = NSWorkspace.OpenConfiguration(); config.activates = true; config.createsNewApplicationInstance = false
            NSWorkspace.shared.openApplication(at: URL(fileURLWithPath: path), configuration: config) { _, error in
                if let error = error { DispatchQueue.main.async { self.notice = error.localizedDescription } }
            }
        }
        setExpanded(false)
    }
    func cancelAppDrag() { if draggedApp != nil { apps = originalApps; draggedApp = nil } }
    func finishAppDrag() { save(); draggedApp = nil; originalApps = [] }
    func toggleLogin() {
        if #available(macOS 13, *) {
            let item = SMAppService.mainApp
            if item.status == .enabled {
                Task { do { try await item.unregister(); await MainActor.run { self.loginEnabled = false } } catch { await MainActor.run { self.notice = error.localizedDescription } } }
            } else if item.status == .requiresApproval { SMAppService.openSystemSettingsLoginItems() }
            else { do { try item.register(); loginEnabled = item.status == .enabled } catch { notice = error.localizedDescription } }
        } else { NSWorkspace.shared.open(URL(string: "x-apple.systempreferences:com.apple.preferences.users")!) }
    }
    static func duration(_ seconds: Double, hundredths: Bool = false) -> String {
        let safe = max(0, seconds), whole = Int(safe)
        let base = String(format: "%02d:%02d:%02d", whole / 3600, whole / 60 % 60, whole % 60)
        return hundredths ? base + String(format: ".%02d", Int(safe * 100) % 100) : base
    }
}

extension NSColor {
    convenience init?(hex: String) {
        let value = hex.trimmingCharacters(in: CharacterSet(charactersIn: "#"))
        guard value.count == 6, let number = UInt32(value, radix: 16) else { return nil }
        self.init(srgbRed: CGFloat((number >> 16) & 255) / 255, green: CGFloat((number >> 8) & 255) / 255, blue: CGFloat(number & 255) / 255, alpha: 1)
    }
    var hex: String {
        let c = usingColorSpace(.deviceRGB) ?? .white
        return String(format: "#%02X%02X%02X", Int((c.redComponent * 255).rounded()), Int((c.greenComponent * 255).rounded()), Int((c.blueComponent * 255).rounded()))
    }
}
