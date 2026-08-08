import AppKit

final class StatusItemView: NSView {
    private let dot = NSView()
    private let percentage = NSTextField(labelWithString: "--%")
    private var dotColor: NSColor = .systemGray

    init() {
        super.init(frame: .zero)
        translatesAutoresizingMaskIntoConstraints = false
        dot.translatesAutoresizingMaskIntoConstraints = false
        dot.wantsLayer = true
        dot.layer?.cornerRadius = 3
        percentage.translatesAutoresizingMaskIntoConstraints = false
        percentage.font = .menuBarFont(ofSize: 0)
        percentage.lineBreakMode = .byClipping
        let stack = NSStackView(views: [dot, percentage])
        stack.translatesAutoresizingMaskIntoConstraints = false
        stack.orientation = .horizontal
        stack.alignment = .centerY
        stack.spacing = 4
        addSubview(stack)
        NSLayoutConstraint.activate([
            dot.widthAnchor.constraint(equalToConstant: 6), dot.heightAnchor.constraint(equalToConstant: 6),
            stack.leadingAnchor.constraint(equalTo: leadingAnchor, constant: 5), stack.trailingAnchor.constraint(equalTo: trailingAnchor, constant: -5),
            stack.centerYAnchor.constraint(equalTo: centerYAnchor), heightAnchor.constraint(equalToConstant: 22)
        ])
        update(percentage: "--%", color: .systemGray, showsStatusDot: false, accessibilityLabel: "Codex 额度暂不可用")
    }

    required init?(coder: NSCoder) { nil }

    func update(percentage text: String, color: NSColor, showsStatusDot: Bool, accessibilityLabel: String) {
        percentage.stringValue = text
        dotColor = color
        dot.layer?.backgroundColor = color.cgColor
        dot.isHidden = !showsStatusDot
        setAccessibilityLabel(accessibilityLabel)
        toolTip = accessibilityLabel
    }
}
