import AppKit

struct PlayingTrack {
    var app: String
    var title: String
    var artist: String
    var playing: Bool
    var position: Double
    var duration: Double
    var artworkURL: String
    var key: String { app + "|" + title + "|" + artist }
}

enum Players {
    static let music = "com.apple.Music"
    static let spotify = "com.spotify.client"
    static let all = [music, spotify]
    static func name(_ id: String) -> String { id == music ? "Apple Music" : "Spotify" }

    static func script(_ body: String, app: String? = nil) -> (NSAppleEventDescriptor?, String?) {
        let wrapped = app.map { "tell application id \"\($0)\"\n\(body)\nend tell" } ?? body
        let source = "with timeout of 3 seconds\n\(wrapped)\nend timeout"
        var error: NSDictionary?
        let result = NSAppleScript(source: source)?.executeAndReturnError(&error)
        return (error == nil ? result : nil, error?[NSAppleScript.errorMessage] as? String)
    }

    static func read(_ app: String) -> (PlayingTrack?, String?) {
        let artwork = app == spotify ? "try\nset u to artwork url of t\nend try" : ""
        let source = """
        if player state is stopped then return {}
        set t to current track
        set u to ""
        \(artwork)
        return {name of t, artist of t, player state is playing, player position, (duration of t) / \(app == spotify ? "1000" : "1"), u}
        """
        let (result, error) = script(source, app: app)
        guard let list = result, list.numberOfItems == 6,
              let title = list.atIndex(1)?.stringValue else { return (nil, error) }
        func number(_ index: Int) -> Double {
            let value = list.atIndex(index)
            if let string = value?.stringValue, let parsed = Double(string) { return parsed }
            return Double(value?.int32Value ?? 0)
        }
        let track = PlayingTrack(app: app, title: title, artist: list.atIndex(2)?.stringValue ?? "",
            playing: list.atIndex(3)?.booleanValue ?? false, position: number(4), duration: number(5),
            artworkURL: list.atIndex(6)?.stringValue ?? "")
        return (track, error)
    }

    static func artwork(_ app: String) -> NSImage? {
        let (value, _) = script("try\nreturn raw data of artwork 1 of current track\non error\nreturn missing value\nend try", app: app)
        guard let data = value?.data, !data.isEmpty else { return nil }
        return NSImage(data: data)
    }
}
