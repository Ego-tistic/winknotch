import AppKit
import SwiftUI

final class FlatSliderCell: NSSliderCell {
    var ink = NSColor.white
    var light = false
    override func drawBar(inside rect: NSRect, flipped: Bool) {
        let knob = knobRect(flipped: flipped)
        let start = rect.minX + 6, end = rect.maxX - 6
        let bar = NSRect(x: start, y: rect.midY - 2, width: max(0, end - start), height: 4)
        NSColor(calibratedWhite: light ? 0.75 : 0.35, alpha: 1).setFill()
        NSBezierPath(roundedRect: bar, xRadius: 2, yRadius: 2).fill()
        ink.setFill()
        NSBezierPath(roundedRect: NSRect(x: start, y: bar.minY, width: max(0, min(end, knob.midX) - start), height: 4), xRadius: 2, yRadius: 2).fill()
    }
    override func drawKnob(_ rect: NSRect) {
        let size: CGFloat = (controlView as? TrackingSlider)?.tracking == true ? 16 : 13
        let frame = NSRect(x: rect.midX - size / 2, y: rect.midY - size / 2, width: size, height: size)
        NSGraphicsContext.saveGraphicsState()
        let shadow = NSShadow(); shadow.shadowBlurRadius = 2; shadow.shadowOffset = NSSize(width: 0, height: -0.5); shadow.shadowColor = NSColor.black.withAlphaComponent(0.2); shadow.set()
        ink.setFill(); NSBezierPath(ovalIn: frame).fill()
        NSGraphicsContext.restoreGraphicsState()
    }
}

final class TrackingSlider: NSSlider {
    var tracking = false
    var editing: ((Bool) -> Void)?
    override func mouseDown(with event: NSEvent) {
        tracking = true; editing?(true); needsDisplay = true
        super.mouseDown(with: event)
        tracking = false; editing?(false); needsDisplay = true
    }
}

struct IslandSlider: NSViewRepresentable {
    @Binding var value: Double
    let range: ClosedRange<Double>
    let editing: (Bool) -> Void
    @Environment(\.colorScheme) var scheme
    init(value: Binding<Double>, in range: ClosedRange<Double>, onEditingChanged: @escaping (Bool) -> Void = { _ in }) {
        _value = value; self.range = range; editing = onEditingChanged
    }
    final class Coordinator: NSObject {
        var parent: IslandSlider
        init(_ parent: IslandSlider) { self.parent = parent }
        @objc func changed(_ slider: NSSlider) { parent.value = slider.doubleValue }
    }
    func makeCoordinator() -> Coordinator { Coordinator(self) }
    func makeNSView(context: Context) -> TrackingSlider {
        let slider = TrackingSlider()
        slider.cell = FlatSliderCell()
        slider.target = context.coordinator; slider.action = #selector(Coordinator.changed(_:)); slider.isContinuous = true
        slider.setContentHuggingPriority(.defaultLow, for: .horizontal)
        return slider
    }
    func updateNSView(_ slider: TrackingSlider, context: Context) {
        context.coordinator.parent = self
        slider.minValue = range.lowerBound; slider.maxValue = range.upperBound
        slider.doubleValue = min(range.upperBound, max(range.lowerBound, value))
        slider.editing = editing
        if let cell = slider.cell as? FlatSliderCell {
            cell.light = scheme == .light
            cell.ink = NSColor(calibratedWhite: cell.light ? 0.07 : 0.97, alpha: 1)
        }
        slider.needsDisplay = true
    }
}
