import Foundation

enum RuntimeProfile {
    #if LITE_BUILD
    static let lite = true
    #else
    static let lite = false
    #endif
    static var name: String { lite ? "WinKnotch Lite" : "WinKnotch" }
    static var timerInterval: Double { lite ? 0.25 : 0.1 }
    static var waveInterval: Double { lite ? 0.25 : 0.05 }
    static func pollInterval(playing: Bool, expanded: Bool) -> Double {
        guard lite else { return 1 }
        return playing ? (expanded ? 1.5 : 2.5) : 5
    }
    static func uiInterval(stopwatch: Bool, expandedMedia: Bool) -> Double {
        guard lite else { return 0 }
        return stopwatch ? 0.1 : expandedMedia ? 0.5 : 1
    }
}
