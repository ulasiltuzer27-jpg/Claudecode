--!strict
-- Theme.luau
-- ==========
-- Arayuzun ortak gorunumu ve kucuk yapi yardimcilari.
--
-- Tek yerde toplanmasinin sebebi: paneller ayri ayri kurulursa her
-- birinde farkli bir gri, farkli bir kose yaricapi olusuyor ve arayuz
-- derme catma gorunuyor. Renk ve olcu kararlari burada, bir kez.

local ReplicatedStorage = game:GetService("ReplicatedStorage")
local Shared = ReplicatedStorage:WaitForChild("VetShared")
local Audio = require(Shared.Audio)

local Theme = {}

Theme.color = {
	panel = Color3.fromRGB(24, 28, 38),
	panelLight = Color3.fromRGB(34, 40, 52),
	panelHover = Color3.fromRGB(44, 52, 66),
	stroke = Color3.fromRGB(62, 72, 90),
	text = Color3.fromRGB(236, 242, 248),
	textDim = Color3.fromRGB(158, 170, 188),
	accent = Color3.fromRGB(72, 176, 176),
	accentDark = Color3.fromRGB(44, 124, 128),
	good = Color3.fromRGB(96, 196, 120),
	warn = Color3.fromRGB(232, 176, 72),
	bad = Color3.fromRGB(228, 96, 92),
	money = Color3.fromRGB(240, 206, 96),
	health = Color3.fromRGB(96, 196, 120),
	stress = Color3.fromRGB(226, 148, 76),
	xp = Color3.fromRGB(110, 168, 240),
}

Theme.font = Enum.Font.GothamMedium
Theme.fontBold = Enum.Font.GothamBold

function Theme.corner(parent: Instance, radius: number): UICorner
	local corner = Instance.new("UICorner")
	corner.CornerRadius = UDim.new(0, radius)
	corner.Parent = parent
	return corner
end

function Theme.stroke(parent: Instance, color: Color3?, thickness: number?): UIStroke
	local stroke = Instance.new("UIStroke")
	stroke.Color = color or Theme.color.stroke
	stroke.Thickness = thickness or 1
	stroke.ApplyStrokeMode = Enum.ApplyStrokeMode.Border
	stroke.Parent = parent
	return stroke
end

function Theme.padding(parent: Instance, amount: number): UIPadding
	local padding = Instance.new("UIPadding")
	padding.PaddingTop = UDim.new(0, amount)
	padding.PaddingBottom = UDim.new(0, amount)
	padding.PaddingLeft = UDim.new(0, amount)
	padding.PaddingRight = UDim.new(0, amount)
	padding.Parent = parent
	return padding
end

function Theme.list(parent: Instance, spacing: number, direction: Enum.FillDirection?): UIListLayout
	local layout = Instance.new("UIListLayout")
	layout.Padding = UDim.new(0, spacing)
	layout.FillDirection = direction or Enum.FillDirection.Vertical
	layout.SortOrder = Enum.SortOrder.LayoutOrder
	layout.Parent = parent
	return layout
end

export type FrameSpec = {
	name: string?,
	size: UDim2,
	position: UDim2?,
	anchor: Vector2?,
	color: Color3?,
	transparency: number?,
	radius: number?,
	parent: Instance?,
	layoutOrder: number?,
	visible: boolean?,
}

function Theme.frame(spec: FrameSpec): Frame
	local frame = Instance.new("Frame")
	frame.Name = spec.name or "Frame"
	frame.Size = spec.size
	frame.Position = spec.position or UDim2.fromOffset(0, 0)
	frame.AnchorPoint = spec.anchor or Vector2.new(0, 0)
	frame.BackgroundColor3 = spec.color or Theme.color.panel
	frame.BackgroundTransparency = spec.transparency or 0.08
	frame.BorderSizePixel = 0
	frame.LayoutOrder = spec.layoutOrder or 0
	frame.Visible = if spec.visible == nil then true else spec.visible
	frame.Parent = spec.parent
	if (spec.radius or 10) > 0 then
		Theme.corner(frame, spec.radius or 10)
	end
	return frame
end

export type LabelSpec = {
	name: string?,
	text: string,
	size: UDim2,
	position: UDim2?,
	anchor: Vector2?,
	color: Color3?,
	textSize: number?,
	bold: boolean?,
	align: Enum.TextXAlignment?,
	parent: Instance?,
	layoutOrder: number?,
	wrapped: boolean?,
}

function Theme.label(spec: LabelSpec): TextLabel
	local label = Instance.new("TextLabel")
	label.Name = spec.name or "Label"
	label.Text = spec.text
	label.Size = spec.size
	label.Position = spec.position or UDim2.fromOffset(0, 0)
	label.AnchorPoint = spec.anchor or Vector2.new(0, 0)
	label.BackgroundTransparency = 1
	label.TextColor3 = spec.color or Theme.color.text
	label.TextSize = spec.textSize or 14
	label.Font = if spec.bold then Theme.fontBold else Theme.font
	label.TextXAlignment = spec.align or Enum.TextXAlignment.Left
	label.TextWrapped = spec.wrapped == true
	label.LayoutOrder = spec.layoutOrder or 0
	label.Parent = spec.parent
	return label
end

export type ButtonSpec = {
	name: string?,
	text: string,
	size: UDim2,
	position: UDim2?,
	anchor: Vector2?,
	color: Color3?,
	textColor: Color3?,
	textSize: number?,
	parent: Instance?,
	layoutOrder: number?,
	onClick: (() -> ())?,
}

function Theme.button(spec: ButtonSpec): TextButton
	local button = Instance.new("TextButton")
	button.Name = spec.name or "Button"
	button.Text = spec.text
	button.Size = spec.size
	button.Position = spec.position or UDim2.fromOffset(0, 0)
	button.AnchorPoint = spec.anchor or Vector2.new(0, 0)
	button.BackgroundColor3 = spec.color or Theme.color.panelLight
	button.BackgroundTransparency = 0
	button.BorderSizePixel = 0
	button.TextColor3 = spec.textColor or Theme.color.text
	button.TextSize = spec.textSize or 14
	button.Font = Theme.fontBold
	button.AutoButtonColor = false
	button.LayoutOrder = spec.layoutOrder or 0
	button.Parent = spec.parent
	Theme.corner(button, 8)

	local base = button.BackgroundColor3
	button.MouseEnter:Connect(function()
		button.BackgroundColor3 = base:Lerp(Color3.new(1, 1, 1), 0.14)
	end)
	button.MouseLeave:Connect(function()
		button.BackgroundColor3 = base
	end)
	-- Tik sesi TEK YERDE: butun dugmeler Theme.button'dan geciyor, o
	-- yuzden her panele ayri ayri ses baglamak gerekmiyor.
	if spec.onClick ~= nil then
		button.Activated:Connect(function()
			Audio.play("uiClick")
			spec.onClick()
		end)
	end
	return button
end

export type Bar = { track: Frame, fill: Frame }

--- Ilerleme/saglik cubugu. `set(ratio)` ile guncelleniyor.
function Theme.bar(parent: Instance, size: UDim2, position: UDim2, color: Color3): Bar
	local track = Theme.frame({
		name = "Track",
		size = size,
		position = position,
		color = Theme.color.panelLight,
		transparency = 0,
		radius = 6,
		parent = parent,
	})
	local fill = Theme.frame({
		name = "Fill",
		size = UDim2.fromScale(0, 1),
		color = color,
		transparency = 0,
		radius = 6,
		parent = track,
	})
	return { track = track, fill = fill }
end

function Theme.setBar(bar: Bar, ratio: number)
	bar.fill.Size = UDim2.fromScale(math.clamp(ratio, 0, 1), 1)
end

--- Kaydirilabilir liste. Panellerin hepsi ayni davranssin diye.
function Theme.scroller(parent: Instance, size: UDim2, position: UDim2): ScrollingFrame
	local scroller = Instance.new("ScrollingFrame")
	scroller.Size = size
	scroller.Position = position
	scroller.BackgroundTransparency = 1
	scroller.BorderSizePixel = 0
	scroller.ScrollBarThickness = 4
	scroller.ScrollBarImageColor3 = Theme.color.stroke
	scroller.AutomaticCanvasSize = Enum.AutomaticSize.Y
	scroller.CanvasSize = UDim2.fromOffset(0, 0)
	scroller.Parent = parent
	return scroller
end

--- Bildirim/durum rengi.
function Theme.kindColor(kind: string): Color3
	if kind == "good" then
		return Theme.color.good
	elseif kind == "bad" then
		return Theme.color.bad
	elseif kind == "warn" then
		return Theme.color.warn
	end
	return Theme.color.accent
end

return Theme
