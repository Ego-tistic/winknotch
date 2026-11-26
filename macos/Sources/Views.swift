import AppKit
import SwiftUI
import UniformTypeIdentifiers

struct GlassMaterial: NSViewRepresentable {
    func makeNSView(context: Context) -> NSVisualEffectView { let view = NSVisualEffectView(); view.material = .hudWindow; view.blendingMode = .behindWindow; view.state = .active; return view }
    func updateNSView(_ view: NSVisualEffectView, context: Context) {}
}

struct IslandOutline: Shape {
    var joined: Bool
    func path(in rect: CGRect) -> Path {
        let radius: CGFloat = joined ? 13 : 25
        if !joined { return Path(roundedRect: rect, cornerRadius: radius) }
        var path = Path()
        let shoulder: CGFloat = 8
        path.move(to: CGPoint(x: 0, y: 0)); path.addLine(to: CGPoint(x: rect.maxX, y: 0))
        path.addQuadCurve(to: CGPoint(x: rect.maxX - shoulder, y: shoulder), control: CGPoint(x: rect.maxX - shoulder, y: 0))
        path.addLine(to: CGPoint(x: rect.maxX - shoulder, y: rect.maxY - radius))
        path.addQuadCurve(to: CGPoint(x: rect.maxX - shoulder - radius, y: rect.maxY), control: CGPoint(x: rect.maxX - shoulder, y: rect.maxY))
        path.addLine(to: CGPoint(x: shoulder + radius, y: rect.maxY))
        path.addQuadCurve(to: CGPoint(x: shoulder, y: rect.maxY - radius), control: CGPoint(x: shoulder, y: rect.maxY))
        path.addLine(to: CGPoint(x: shoulder, y: shoulder))
        path.addQuadCurve(to: .zero, control: CGPoint(x: shoulder, y: 0)); path.closeSubpath()
        return path
    }
}

struct IslandButton: View {
    let title: String
    let symbol: String
    var prominent = false
    let action: () -> Void
    @State private var hover = false
    var body: some View {
        Button(action: action) {
            HStack(spacing: 5) { Image(systemName: symbol); if !title.isEmpty { Text(title).font(.system(size: 11, weight: .medium)) } }
                .frame(minWidth: 18, minHeight: 20).padding(.horizontal, prominent ? 12 : 7).padding(.vertical, 5)
                .background(Color.primary.opacity(hover ? 0.16 : 0.08)).cornerRadius(9)
        }.buttonStyle(.plain).scaleEffect(hover ? 1.10 : 1)
            .animation(.easeOut(duration: 0.18), value: hover).onHover { hover = $0 }
            .help(title.isEmpty ? symbol : title)
    }
}

struct AlbumArt: View {
    @ObservedObject var model: IslandModel
    var size: CGFloat
    var body: some View {
        Group {
            if let image = model.artwork { Image(nsImage: image).resizable().scaledToFill() }
            else { ZStack { model.ink.opacity(0.08); Image(systemName: "music.note").foregroundColor(model.secondary) } }
        }.frame(width: size, height: size).clipShape(RoundedRectangle(cornerRadius: size > 30 ? 9 : 5))
    }
}

struct MusicWaves: View {
    @ObservedObject var model: IslandModel
    var body: some View {
        TimelineView(.animation(minimumInterval: 1.0 / 20, paused: model.track?.playing != true)) { context in
            HStack(alignment: .center, spacing: 2) {
                ForEach(0..<12) { index in
                    let phase = context.date.timeIntervalSinceReferenceDate * 6 - Double(index) * 0.65
                    let height = model.track?.playing == true ? 4 + 13 * abs(sin(phase) * cos(phase * 0.43)) : 3
                    Capsule().fill(model.artwork == nil ? model.ink : Color(model.accent)).frame(width: 2, height: height)
                }
            }.frame(width: 46, height: 20)
        }
    }
}

struct IslandRoot: View {
    @ObservedObject var model: IslandModel
    var joined: Bool { !model.expanded && model.placement == "Top" }
    var body: some View {
        ZStack {
            GlassMaterial().opacity(model.opacity)
            Color(model.tint).opacity(model.opacity)
            if model.expanded && model.page == .media, let image = model.artwork {
                GeometryReader { geometry in Image(nsImage: image).resizable().scaledToFill().frame(width: geometry.size.width, height: geometry.size.height).blur(radius: 12).opacity(0.30).clipped() }
            }
            if model.expanded {
                VStack(spacing: 8) {
                    if model.tabsOpen {
                        HStack(spacing: 12) {
                            ForEach([IslandPage.media, .clock, .apps], id: \.self) { page in
                                Button(page.rawValue) { model.select(page) }.buttonStyle(.plain).padding(.horizontal, 10).padding(.vertical, 5)
                                    .background(model.page == page ? model.ink.opacity(0.14) : Color.clear).cornerRadius(8)
                            }
                        }.font(.system(size: 11, weight: .medium)).padding(.top, 4)
                    }
                    Group {
                        switch model.page {
                        case .media: MediaPage(model: model)
                        case .clock: ClockPage(model: model)
                        case .apps: AppsPage(model: model)
                        case .settings: SettingsPage(model: model)
                        }
                    }.frame(maxWidth: .infinity, maxHeight: .infinity)
                    HStack {
                        IslandButton(title: "", symbol: "square.grid.3x3") { withAnimation { model.tabsOpen.toggle() }; model.resize?() }.help("Show tabs")
                        Spacer()
                        if model.ringing != nil { Button("Dismiss", action: model.dismiss).buttonStyle(.plain) }
                        Spacer()
                        IslandButton(title: "", symbol: "gearshape") { model.select(.settings) }.help("Settings")
                    }.frame(height: 27)
                }.padding(14).transition(.opacity.combined(with: .scale(scale: 0.96)))
            } else {
                HStack(spacing: 12) {
                    if let ringing = model.ringing {
                        Text(ringing).font(.system(size: 12, weight: .semibold)); Button("Dismiss", action: model.dismiss).buttonStyle(.plain)
                    } else if model.countdownEnd != nil {
                        Image(systemName: "timer"); Text(IslandModel.duration(ceil(model.countdown))).monospacedDigit()
                    } else if model.compactClock {
                        Spacer(minLength: 0); Text(model.clockText).font(.system(size: 18, weight: .semibold, design: .rounded)).monospacedDigit(); Spacer(minLength: 0)
                    } else {
                        AlbumArt(model: model, size: 25); Spacer(); MusicWaves(model: model)
                    }
                }.padding(.horizontal, 15)
            }
            if model.flash { Color.white.opacity(0.8).allowsHitTesting(false) }
        }.foregroundColor(model.ink).accentColor(model.lightSurface ? Color(white: 0.18) : .white)
            .preferredColorScheme(model.lightSurface ? .light : .dark)
            .clipShape(IslandOutline(joined: joined))
            .overlay(IslandOutline(joined: joined).stroke(model.ink.opacity(0.12), lineWidth: 0.7))
            .onHover { inside in
                model.hovering = inside
                if inside { model.setExpanded(true) }
            }
    }
}

struct ScaledIslandRoot: View {
    @ObservedObject var model: IslandModel
    var body: some View {
        GeometryReader { geometry in
            IslandRoot(model: model)
                .frame(width: geometry.size.width / model.scale, height: geometry.size.height / model.scale)
                .scaleEffect(model.scale, anchor: .topLeading)
        }
    }
}

struct MediaPage: View {
    @ObservedObject var model: IslandModel
    var body: some View {
        VStack(spacing: 11) {
            Button(action: model.openPlayer) {
                HStack(spacing: 12) {
                    AlbumArt(model: model, size: 49)
                    VStack(alignment: .leading, spacing: 4) {
                        Text(model.track?.title ?? "Nothing playing").font(.system(size: 15, weight: .semibold)).lineLimit(1)
                        Text(model.track?.artist ?? "Connect a player to get started").font(.system(size: 11)).foregroundColor(model.secondary).lineLimit(1)
                    }
                    Spacer(minLength: 0)
                }
            }.buttonStyle(.plain)
            HStack(spacing: 23) {
                IslandButton(title: "", symbol: "backward.end.fill") { model.command("previous track") }
                IslandButton(title: "", symbol: model.track?.playing == true ? "pause.fill" : "play.fill", prominent: true) { model.command("playpause") }
                IslandButton(title: "", symbol: "forward.end.fill") { model.command("next track") }
            }.disabled(model.track == nil)
            VStack(spacing: 0) {
                IslandSlider(value: $model.position, in: 0...max(1, model.track?.duration ?? 1), onEditingChanged: { editing in model.seeking = editing; if !editing { model.seek() } }).disabled(model.track == nil)
                HStack { Text(IslandModel.duration(model.position)); Spacer(); Text(IslandModel.duration(model.track?.duration ?? 0)) }.font(.system(size: 9, design: .monospaced)).foregroundColor(model.secondary)
            }
            HStack { Image(systemName: model.volume == 0 ? "speaker.slash.fill" : model.volume < 50 ? "speaker.wave.1.fill" : "speaker.wave.3.fill"); IslandSlider(value: $model.volume, in: 0...100, onEditingChanged: { if !$0 { model.setVolume() } }) }.font(.system(size: 11))
            if model.track == nil {
                HStack(spacing: 12) {
                    Button("Connect Music") { model.connect(Players.music) }; Button("Connect Spotify") { model.connect(Players.spotify) }
                }.font(.system(size: 11)).buttonStyle(.plain)
            }
        }
    }
}

struct TimeFields: View {
    @Binding var hours: String
    @Binding var minutes: String
    @Binding var seconds: String
    var body: some View {
        HStack(spacing: 7) {
            field("Hours", $hours); Text(":"); field("Minutes", $minutes); Text(":"); field("Seconds", $seconds)
        }
    }
    private func field(_ caption: String, _ binding: Binding<String>) -> some View {
        VStack(spacing: 4) {
            TextField("00", text: binding).font(.system(size: 25, weight: .medium, design: .rounded)).multilineTextAlignment(.center).textFieldStyle(.plain).frame(width: 57).padding(.vertical, 8).background(Color.primary.opacity(0.07)).cornerRadius(10)
            Text(caption).font(.system(size: 9)).opacity(0.65)
        }
    }
}

struct ClockPage: View {
    @ObservedObject var model: IslandModel
    var body: some View {
        VStack(spacing: 12) {
            if let ringing = model.ringing { Text(ringing).font(.headline); Button("Dismiss alarm", action: model.dismiss) }
            Picker("Clock tool", selection: $model.tool) { ForEach(ClockTool.allCases, id: \.self) { Text($0.rawValue).tag($0) } }.pickerStyle(.segmented).labelsHidden().onChange(of: model.tool) { _ in model.resize?() }
            switch model.tool {
            case .clock:
                Text(model.clockText).font(.system(size: 35, weight: .medium, design: .rounded)).monospacedDigit().minimumScaleFactor(0.7).lineLimit(1)
                Text(model.now, style: .date).font(.system(size: 12)).foregroundColor(model.secondary)
            case .stopwatch:
                Text(IslandModel.duration(model.stopwatchValue, hundredths: true)).font(.system(size: 28, weight: .medium, design: .rounded)).monospacedDigit()
                HStack(spacing: 18) {
                    IslandButton(title: model.stopwatchStart == nil ? "Start" : "Pause", symbol: model.stopwatchStart == nil ? "play.fill" : "pause.fill", action: model.toggleStopwatch)
                    IslandButton(title: "Reset", symbol: "arrow.counterclockwise") { model.stopwatchElapsed = 0; model.stopwatchStart = nil }
                }
            case .timer:
                if model.countdownEnd != nil {
                    Text(IslandModel.duration(ceil(model.countdown))).font(.system(size: 30, weight: .medium, design: .rounded)).monospacedDigit()
                    IslandButton(title: "Cancel timer", symbol: "xmark") { model.countdownEnd = nil }
                } else {
                    TimeFields(hours: $model.timerHours, minutes: $model.timerMinutes, seconds: $model.timerSeconds)
                    IslandButton(title: "Start timer", symbol: "timer", action: model.startTimer)
                }
            case .wake:
                if let date = model.wakeDate {
                    Text("Alarm set for").font(.caption).foregroundColor(model.secondary)
                    Text(date, style: .time).font(.title2)
                    IslandButton(title: "Cancel alarm", symbol: "xmark") { model.wakeDate = nil }
                } else {
                    Text("Time of day · 24-hour input").font(.caption).foregroundColor(model.secondary)
                    TimeFields(hours: $model.wakeHours, minutes: $model.wakeMinutes, seconds: $model.wakeSeconds)
                    IslandButton(title: "Set wake-up alarm", symbol: "alarm", action: model.setWake)
                }
            }
            if !model.notice.isEmpty { Text(model.notice).font(.caption).foregroundColor(model.secondary).lineLimit(2) }
        }.frame(maxHeight: .infinity)
    }
}

let dockItemType = "io.github.ego-tistic.winknotch.dock-app"

struct DockDrop: DropDelegate {
    let target: String
    let model: IslandModel
    func dropEntered(info: DropInfo) {
        guard let source = model.draggedApp, source != target, let from = model.apps.firstIndex(of: source), let to = model.apps.firstIndex(of: target) else { return }
        withAnimation(.easeInOut(duration: 0.18)) { let item = model.apps.remove(at: from); model.apps.insert(item, at: to) }
    }
    func dropUpdated(info: DropInfo) -> DropProposal? { DropProposal(operation: .move) }
    func performDrop(info: DropInfo) -> Bool { model.finishAppDrag(); return true }
}

struct DockApp: View {
    @ObservedObject var model: IslandModel
    let path: String
    @State private var hover = false
    var body: some View {
        Button { model.openApp(path) } label: {
            VStack(spacing: 5) {
                Image(nsImage: NSWorkspace.shared.icon(forFile: path)).resizable().interpolation(.high).frame(width: 34, height: 34)
                if model.showAppNames { Text(model.appName(path)).font(.system(size: 9)).lineLimit(1).frame(width: 55) }
            }.frame(width: 58, height: model.showAppNames ? 60 : 42)
        }.buttonStyle(.plain).scaleEffect(hover ? 1.18 : 1, anchor: .bottom).offset(y: hover ? -4 : 0)
            .animation(.easeOut(duration: 0.18), value: hover).onHover { hover = $0 }.help(model.appName(path) + " · drag to rearrange")
            .onDrag {
                model.originalApps = model.apps; model.draggedApp = path
                let provider = NSItemProvider()
                provider.registerDataRepresentation(forTypeIdentifier: dockItemType, visibility: .ownProcess) { completion in completion(Data(path.utf8), nil); return nil }
                return provider
            }.onDrop(of: [dockItemType], delegate: DockDrop(target: path, model: model))
    }
}

struct AppsPage: View {
    @ObservedObject var model: IslandModel
    var body: some View {
        VStack(spacing: 10) {
            HStack {
                Image(systemName: "magnifyingglass")
                TextField("Search apps", text: $model.search).textFieldStyle(.plain)
                IslandButton(title: "", symbol: "slider.horizontal.3") { model.editingApps.toggle(); model.resize?() }
            }
            if model.editingApps {
                Picker("Layout", selection: $model.appLayout) { ForEach(["Dock", "Grid", "List"], id: \.self) { Text($0) } }.pickerStyle(.segmented).labelsHidden()
                Toggle("Show app names", isOn: $model.showAppNames).font(.caption)
                ScrollView { VStack(spacing: 7) { ForEach(model.apps, id: \.self) { path in HStack { Text(model.appName(path)).lineLimit(1); Spacer(); Button { model.apps.removeAll { $0 == path }; model.save() } label: { Image(systemName: "minus.circle") }.buttonStyle(.plain) } } } }
                Button("Add applications…", action: model.addApps)
            } else if model.apps.isEmpty {
                Text("Your apps, one click away.").font(.caption).foregroundColor(model.secondary)
                Button("Choose apps", action: model.addApps)
            } else if model.appLayout == "Dock" {
                ScrollView(.horizontal, showsIndicators: false) {
                    HStack(alignment: .bottom, spacing: 7) { ForEach(model.selectedApps, id: \.self) { DockApp(model: model, path: $0) } }
                        .padding(.horizontal, 13).padding(.top, 12).padding(.bottom, 7).frame(minWidth: 360)
                }.background(model.ink.opacity(0.07)).cornerRadius(15)
            } else {
                ScrollView {
                    if model.appLayout == "Grid" { LazyVGrid(columns: Array(repeating: GridItem(.flexible(), spacing: 12), count: 3), spacing: 14) { ForEach(model.selectedApps, id: \.self) { DockApp(model: model, path: $0) } }.padding(.top, 7) }
                    else { VStack(spacing: 10) { ForEach(model.selectedApps, id: \.self) { path in Button { model.openApp(path) } label: { HStack { Image(nsImage: NSWorkspace.shared.icon(forFile: path)).resizable().frame(width: 26, height: 26); if model.showAppNames { Text(model.appName(path)) }; Spacer() } }.buttonStyle(.plain) } } }
                }
            }
        }.onChange(of: model.appLayout) { _ in model.save(); model.resize?() }.onChange(of: model.showAppNames) { _ in model.save() }
    }
}

struct SettingsPage: View {
    @ObservedObject var model: IslandModel
    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 12) {
                Text("Settings").font(.system(size: 20, weight: .semibold))
                section("Appearance") {
                    Picker("Theme", selection: $model.theme) { ForEach(["Dark", "Light", "Custom"], id: \.self) { Text($0) } }.pickerStyle(.segmented)
                    if model.theme == "Custom" { ColorBlockPicker(color: $model.customColor) }
                    Text("Opacity  \(Int(model.opacity * 100))%").font(.caption)
                    IslandSlider(value: $model.opacity, in: 0...1)
                    Text("Island size  \(Int(model.scale * 100))%").font(.caption)
                    IslandSlider(value: $model.scale, in: 0.6...1.5)
                }
                section("Position") {
                    Picker("Placement", selection: $model.placement) { ForEach(["Top", "Bottom", "Floating"], id: \.self) { Text($0) } }
                    Text("In Floating mode, drag an empty part of the island to move it.").font(.caption2).foregroundColor(model.secondary)
                    Toggle("Hide automatically", isOn: $model.autoHide)
                    Button("Re-center island") { model.recover?() }
                }
                section("Clock & sound") {
                    Toggle("24-hour time", isOn: $model.militaryTime)
                    HStack { Picker("Alarm sound", selection: $model.selectedSound) { ForEach(["Glass", "Ping", "Submarine", "Sosumi"], id: \.self) { Text($0) } }; Button("Preview", action: model.previewSound) }
                }
                section("Music players") {
                    Button(model.enabledPlayers.contains(Players.music) ? "Reconnect Apple Music" : "Connect Apple Music") { model.connect(Players.music) }
                    Button(model.enabledPlayers.contains(Players.spotify) ? "Reconnect Spotify" : "Connect Spotify") { model.connect(Players.spotify) }
                    if !model.playerError.isEmpty { Text(model.playerError).font(.caption).foregroundColor(model.secondary) }
                    Text("Approve Automation access when macOS asks. Only connected players are read.").font(.caption2).foregroundColor(model.secondary)
                }
                section("System") {
                    Button(model.loginEnabled ? "Disable Start at Login" : "Start at Login", action: model.toggleLogin)
                    Text("Find the island anytime from the waveform icon in the menu bar.").font(.caption2).foregroundColor(model.secondary)
                }
            }.padding(.trailing, 5)
        }
        .onChange(of: model.theme) { _ in model.save() }
        .onChange(of: model.customColor) { _ in model.save() }
        .onChange(of: model.opacity) { _ in model.save() }
        .onChange(of: model.scale) { _ in model.save(); model.resize?() }
        .onChange(of: model.militaryTime) { _ in model.save() }
        .onChange(of: model.autoHide) { _ in model.save(); model.resize?() }
        .onChange(of: model.placement) { _ in model.save(); model.resize?() }
        .onChange(of: model.selectedSound) { _ in model.save() }
    }
    private func section<Content: View>(_ title: String, @ViewBuilder content: () -> Content) -> some View {
        VStack(alignment: .leading, spacing: 10) { Text(title).font(.system(size: 12, weight: .semibold)).foregroundColor(model.secondary); content() }
            .padding(12).frame(maxWidth: .infinity, alignment: .leading).background(model.ink.opacity(0.05)).cornerRadius(12)
    }
}

struct ColorBlockPicker: View {
    @Binding var color: Color
    @State private var brightness = 1.0
    @State private var hex = ""
    var body: some View {
        VStack(spacing: 7) {
            ColorBlockRepresentable(color: $color).frame(height: 105).clipShape(RoundedRectangle(cornerRadius: 7))
            HStack { Text("Brightness").font(.caption); IslandSlider(value: $brightness, in: 0...1).onChange(of: brightness) { value in
                guard let c = NSColor(color).usingColorSpace(.sRGB) else { return }
                color = Color(NSColor(calibratedHue: c.hueComponent, saturation: c.saturationComponent, brightness: value, alpha: 1))
            } }
            TextField("#526D9A", text: $hex, onCommit: { if let value = NSColor(hex: hex) { color = Color(value) } }).font(.system(size: 11, design: .monospaced))
        }.onAppear { sync() }.onChange(of: color) { _ in sync() }
    }
    private func sync() { let c = NSColor(color).usingColorSpace(.sRGB) ?? .white; brightness = Double(c.brightnessComponent); hex = c.hex }
}

struct ColorBlockRepresentable: NSViewRepresentable {
    @Binding var color: Color
    func makeNSView(context: Context) -> ColorBlockNSView { let view = ColorBlockNSView(); view.changed = { color = Color($0) }; return view }
    func updateNSView(_ view: ColorBlockNSView, context: Context) { view.color = NSColor(color); view.needsDisplay = true }
}
final class ColorBlockNSView: NSView {
    var color: NSColor = .systemBlue
    var changed: ((NSColor) -> Void)?
    override var isFlipped: Bool { true }
    override func draw(_ dirtyRect: NSRect) {
        let c = color.usingColorSpace(.sRGB) ?? .white
        let value = c.brightnessComponent
        let width = 128, height = 50
        guard let bitmap = NSBitmapImageRep(bitmapDataPlanes: nil, pixelsWide: width, pixelsHigh: height, bitsPerSample: 8, samplesPerPixel: 4, hasAlpha: true, isPlanar: false, colorSpaceName: .deviceRGB, bytesPerRow: width * 4, bitsPerPixel: 32) else { return }
        for y in 0..<height { for x in 0..<width { bitmap.setColor(NSColor(calibratedHue: CGFloat(x) / CGFloat(width), saturation: CGFloat(y) / CGFloat(height - 1), brightness: value, alpha: 1), atX: x, y: y) } }
        bitmap.draw(in: bounds)
        let point = NSPoint(x: c.hueComponent * bounds.width, y: c.saturationComponent * bounds.height)
        let circle = NSBezierPath(ovalIn: NSRect(x: point.x - 5, y: point.y - 5, width: 10, height: 10))
        NSColor.black.setStroke(); circle.lineWidth = 4; circle.stroke(); NSColor.white.setStroke(); circle.lineWidth = 2; circle.stroke()
    }
    override func mouseDown(with event: NSEvent) { pick(event) }
    override func mouseDragged(with event: NSEvent) { pick(event) }
    private func pick(_ event: NSEvent) {
        let point = convert(event.locationInWindow, from: nil), c = color.usingColorSpace(.sRGB) ?? .white
        let value = NSColor(calibratedHue: min(0.999, max(0, point.x / max(1, bounds.width))), saturation: min(1, max(0, point.y / max(1, bounds.height))), brightness: c.brightnessComponent, alpha: 1)
        changed?(value)
    }
}
