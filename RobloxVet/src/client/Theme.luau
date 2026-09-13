--!strict
-- Theme.luau
-- ==========
-- Arayuzun TASARIM SISTEMI: olcekler, koyu cam yuzey, ikonlar, animasyon.
--
-- Neden tek dosya?
-- ----------------
-- Paneller ayri ayri kurulursa her birinde farkli bir gri, farkli bir kose
-- yaricapi, farkli bir yazi boyu olusuyor ve arayuz derme catma gorunuyor.
-- Renk, olcu ve HAREKET kararlari burada, bir kez veriliyor.
--
-- Gorsel asset YOK
-- ----------------
-- Bu depoyu ureten ortamda Roblox'a erisim yoktu; bir ikon dokusu (Image)
-- yuklemek mumkun degil, yuklense de dogrulanamazdi. Bu yuzden butun
-- ikonlar `Frame`'lerden (dikdortgen + daire + dondurme) kuruluyor. Hicbir
-- ikon yazi tipine ya da dokuya bagli degil: ne yazarsak ekranda o cikiyor.
--
-- Animasyon tek noktadan
-- ----------------------
-- Her panel kendi TweenInfo'sunu yazarsa biri 0.1 sn digeri 0.6 sn suren
-- gecisler olusur ve arayuz tutarsiz hissettirir. Butun gecisler
-- `Theme.tween` uzerinden, `Theme.motion` suresleriyle gidiyor.

local TweenService = game:GetService("TweenService")

local ReplicatedStorage = game:GetService("ReplicatedStorage")
local Shared = ReplicatedStorage:WaitForChild("VetShared")
local Audio = require(Shared.Audio)

local Theme = {}

-- ── Renkler ───────────────────────────────────────────────────────────
Theme.color = {
	panel = Color3.fromRGB(20, 24, 33),
	panelLight = Color3.fromRGB(32, 38, 50),
	panelHover = Color3.fromRGB(44, 52, 66),
	panelDeep = Color3.fromRGB(13, 16, 23),
	stroke = Color3.fromRGB(62, 72, 90),
	strokeSoft = Color3.fromRGB(46, 54, 70),
	text = Color3.fromRGB(236, 242, 248),
	textDim = Color3.fromRGB(158, 170, 188),
	textFaint = Color3.fromRGB(110, 122, 140),
	accent = Color3.fromRGB(72, 176, 176),
	accentDark = Color3.fromRGB(44, 124, 128),
	accentGlow = Color3.fromRGB(120, 226, 224),
	good = Color3.fromRGB(96, 196, 120),
	warn = Color3.fromRGB(232, 176, 72),
	bad = Color3.fromRGB(228, 96, 92),
	money = Color3.fromRGB(240, 206, 96),
	health = Color3.fromRGB(96, 196, 120),
	stress = Color3.fromRGB(226, 148, 76),
	xp = Color3.fromRGB(110, 168, 240),
	rep = Color3.fromRGB(168, 140, 232),
	shadow = Color3.fromRGB(0, 0, 0),
}

-- ── Olcekler ──────────────────────────────────────────────────────────
-- Panellerin hepsi ayni ritmi kullansin diye boslugun ve yaricapin
-- serbest sayi olmasi yasak: yalnizca bu degerler.
Theme.space = { xs = 4, sm = 8, md = 12, lg = 16, xl = 24 }
Theme.radius = { sm = 6, md = 10, lg = 14 }
Theme.size = { micro = 11, small = 13, body = 15, title = 18, big = 22, hero = 34 }

Theme.font = Enum.Font.GothamMedium
Theme.fontBold = Enum.Font.GothamBold

-- ── Hareket ───────────────────────────────────────────────────────────
Theme.motion = { quick = 0.12, normal = 0.22, slow = 0.4, count = 0.45 }

local EASE_OUT = TweenInfo.new(Theme.motion.normal, Enum.EasingStyle.Quint, Enum.EasingDirection.Out)

--- TEK animasyon noktasi. Suresi verilmezse standart gecis kullanilir.
function Theme.tween(target: Instance, properties: { [string]: any }, duration: number?, style: Enum.EasingStyle?): Tween
	local info = if duration == nil and style == nil
		then EASE_OUT
		else TweenInfo.new(
			duration or Theme.motion.normal,
			style or Enum.EasingStyle.Quint,
			Enum.EasingDirection.Out
		)
	local tween = TweenService:Create(target, info, properties)
	tween:Play()
	return tween
end

export type Pulse = { stop: () -> () }

--- Kritik durumda nabiz atan deger (ileri-geri suren tween).
--- Donen tutamactan `stop()` cagrilinca ozellik baslangic degerine doner.
function Theme.pulse(target: Instance, property: string, to: any, period: number?): Pulse
	local original = (target :: any)[property]
	local info = TweenInfo.new(
		period or 0.55,
		Enum.EasingStyle.Sine,
		Enum.EasingDirection.InOut,
		-1,
		true
	)
	local tween = TweenService:Create(target, info, { [property] = to })
	tween:Play()
	local stopped = false
	return {
		stop = function()
			if stopped then
				return
			end
			stopped = true
			tween:Cancel()
			if (target :: any).Parent ~= nil then
				(target :: any)[property] = original
			end
		end,
	}
end

-- ── Kucuk yapi yardimcilari ───────────────────────────────────────────
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

function Theme.padding(parent: Instance, amount: number, horizontal: number?): UIPadding
	local padding = Instance.new("UIPadding")
	padding.PaddingTop = UDim.new(0, amount)
	padding.PaddingBottom = UDim.new(0, amount)
	padding.PaddingLeft = UDim.new(0, horizontal or amount)
	padding.PaddingRight = UDim.new(0, horizontal or amount)
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

function Theme.grid(parent: Instance, cell: UDim2, spacing: UDim2): UIGridLayout
	local layout = Instance.new("UIGridLayout")
	layout.CellSize = cell
	layout.CellPadding = spacing
	layout.SortOrder = Enum.SortOrder.LayoutOrder
	layout.Parent = parent
	return layout
end

--- Dikey renk gecisi. Koyu cam hissi bundan geliyor: ustte biraz acik,
--- altta koyu. Tek renk duz bir dikdortgen "plastik" duruyor.
function Theme.gradient(parent: Instance, top: Color3, bottom: Color3, rotation: number?): UIGradient
	local gradient = Instance.new("UIGradient")
	gradient.Color = ColorSequence.new({
		ColorSequenceKeypoint.new(0, top),
		ColorSequenceKeypoint.new(1, bottom),
	})
	gradient.Rotation = rotation or 90
	gradient.Parent = parent
	return gradient
end

-- ── Yuzey ─────────────────────────────────────────────────────────────
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
	zIndex: number?,
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
	frame.ZIndex = spec.zIndex or 1
	frame.Visible = if spec.visible == nil then true else spec.visible
	frame.Parent = spec.parent
	if (spec.radius or Theme.radius.md) > 0 then
		Theme.corner(frame, spec.radius or Theme.radius.md)
	end
	return frame
end

--- Koyu CAM yuzey: gecisli zemin, 1px kenar, ustte ince isik cizgisi,
--- altta koyu rim. Dordu birlikte yukseklik hissi veriyor; tek basina
--- hicbiri vermiyor.
function Theme.glass(frame: Frame, accent: Color3?): Frame
	frame.BackgroundColor3 = Theme.color.panel
	frame.BackgroundTransparency = 0.06
	Theme.gradient(frame, Theme.color.panelLight, Theme.color.panelDeep)
	Theme.stroke(frame, accent or Theme.color.strokeSoft, 1)

	local highlight = Instance.new("Frame")
	highlight.Name = "UstCizgi"
	highlight.Size = UDim2.new(1, -Theme.space.lg, 0, 1)
	highlight.Position = UDim2.new(0, Theme.space.sm, 0, 1)
	highlight.BackgroundColor3 = accent or Theme.color.text
	highlight.BackgroundTransparency = 0.82
	highlight.BorderSizePixel = 0
	highlight.ZIndex = frame.ZIndex
	highlight.Parent = frame

	local rim = Instance.new("Frame")
	rim.Name = "AltRim"
	rim.Size = UDim2.new(1, 0, 0, 3)
	rim.Position = UDim2.new(0, 0, 1, -3)
	rim.BackgroundColor3 = Theme.color.shadow
	rim.BackgroundTransparency = 0.55
	rim.BorderSizePixel = 0
	rim.ZIndex = frame.ZIndex
	rim.Parent = frame
	Theme.corner(rim, Theme.radius.sm)

	return frame
end

--- Cam yuzeyli kart. `Theme.frame` + `Theme.glass`, tek cagri.
function Theme.card(spec: FrameSpec, accent: Color3?): Frame
	return Theme.glass(Theme.frame(spec), accent)
end

-- ── Yazi ──────────────────────────────────────────────────────────────
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
	verticalAlign: Enum.TextYAlignment?,
	parent: Instance?,
	layoutOrder: number?,
	wrapped: boolean?,
	zIndex: number?,
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
	label.TextSize = spec.textSize or Theme.size.body
	label.Font = if spec.bold then Theme.fontBold else Theme.font
	label.TextXAlignment = spec.align or Enum.TextXAlignment.Left
	label.TextYAlignment = spec.verticalAlign or Enum.TextYAlignment.Center
	label.TextWrapped = spec.wrapped == true
	label.LayoutOrder = spec.layoutOrder or 0
	label.ZIndex = spec.zIndex or 1
	label.Parent = spec.parent
	return label
end

-- ── Ikonlar ───────────────────────────────────────────────────────────
-- Her ikon 0..1 birim karede tanimli sekillerden kuruluyor.
-- `k` = kare, `d` = daire (tam yuvarlak kose).
-- Alanlar: sekil, x, y, genislik, yukseklik, dondurme, saydamlik.
type Shape = { string, number, number, number, number, number?, number? }

local ICONS: { [string]: { Shape } } = {
	-- Para: dis daire + ic halka izlenimi veren kucuk daire
	money = {
		{ "d", 0.08, 0.08, 0.84, 0.84 },
		{ "d", 0.26, 0.26, 0.48, 0.48, 0, 0.75 },
		{ "k", 0.46, 0.18, 0.08, 0.64 },
	},
	-- Seviye: elmas (45 derece dondurulmus kare)
	level = {
		{ "k", 0.22, 0.22, 0.56, 0.56, 45 },
	},
	-- Itibar: yildizi taklit eden ic ice iki elmas
	reputation = {
		{ "k", 0.18, 0.18, 0.64, 0.64, 45 },
		{ "k", 0.32, 0.32, 0.36, 0.36, 0, 0.6 },
	},
	-- Kuyruk: ust uste uc cizgi
	queue = {
		{ "k", 0.1, 0.18, 0.8, 0.14 },
		{ "k", 0.1, 0.43, 0.8, 0.14 },
		{ "k", 0.1, 0.68, 0.8, 0.14 },
	},
	-- Saglik: arti
	health = {
		{ "k", 0.4, 0.08, 0.2, 0.84 },
		{ "k", 0.08, 0.4, 0.84, 0.2 },
	},
	-- Stres: yukari bakan ucgen hissi (dondurulmus kare + taban)
	stress = {
		{ "k", 0.28, 0.12, 0.44, 0.44, 45 },
		{ "k", 0.44, 0.52, 0.12, 0.36 },
	},
	-- XP: yukari ok (iki egik cubuk)
	xp = {
		{ "k", 0.2, 0.42, 0.46, 0.14, -45 },
		{ "k", 0.34, 0.42, 0.46, 0.14, 45 },
		{ "k", 0.43, 0.3, 0.14, 0.6 },
	},
	-- Onay: iki egik cubuk
	check = {
		{ "k", 0.14, 0.52, 0.32, 0.14, -45 },
		{ "k", 0.36, 0.24, 0.52, 0.14, 45 },
	},
	-- Ret: carpi
	cross = {
		{ "k", 0.12, 0.43, 0.76, 0.14, 45 },
		{ "k", 0.12, 0.43, 0.76, 0.14, -45 },
	},
	-- Kilit: govde + kemer
	lock = {
		{ "k", 0.18, 0.44, 0.64, 0.44 },
		{ "k", 0.3, 0.14, 0.4, 0.34, 0, 0.5 },
		{ "k", 0.44, 0.56, 0.12, 0.2, 0, 0.4 },
	},
	-- Saat: daire + iki ibre
	clock = {
		{ "d", 0.06, 0.06, 0.88, 0.88, 0, 0.55 },
		{ "k", 0.46, 0.2, 0.08, 0.34 },
		{ "k", 0.46, 0.46, 0.3, 0.08 },
	},
	-- Pati: bir avuc + uc parmak
	paw = {
		{ "d", 0.22, 0.42, 0.56, 0.46 },
		{ "d", 0.12, 0.2, 0.22, 0.26 },
		{ "d", 0.39, 0.08, 0.22, 0.26 },
		{ "d", 0.66, 0.2, 0.22, 0.26 },
	},
	-- Alet: sap + uc
	tool = {
		{ "k", 0.44, 0.32, 0.12, 0.6 },
		{ "k", 0.26, 0.1, 0.48, 0.24 },
	},
	-- Uyari: unlem
	alert = {
		{ "k", 0.43, 0.1, 0.14, 0.5 },
		{ "k", 0.43, 0.7, 0.14, 0.16 },
	},
	-- Bilgi: nokta + cubuk
	info = {
		{ "k", 0.43, 0.12, 0.14, 0.16 },
		{ "k", 0.43, 0.36, 0.14, 0.5 },
	},
	-- Magaza: tezgah + tente
	shop = {
		{ "k", 0.12, 0.4, 0.76, 0.48 },
		{ "k", 0.06, 0.16, 0.88, 0.2 },
	},
	-- Dekor: saksi + govde
	decor = {
		{ "d", 0.3, 0.08, 0.4, 0.4 },
		{ "k", 0.44, 0.38, 0.12, 0.26 },
		{ "k", 0.24, 0.6, 0.52, 0.3 },
	},
	-- Gece: ay hissi (daire + ustune binen ikinci daire)
	night = {
		{ "d", 0.1, 0.1, 0.7, 0.7 },
		{ "d", 0.34, 0.02, 0.62, 0.62, 0, 1 },
	},
	-- ── Alet ikonlari (adlari alet kimlikleriyle ayni) ────────────────
	-- Gozle muayene: goz (dis oval + bebek)
	visual = {
		{ "d", 0.04, 0.28, 0.92, 0.44, 0, 0.5 },
		{ "d", 0.34, 0.32, 0.32, 0.36 },
	},
	-- Steteskop: kulaklik kollari + gogus parcasi
	stethoscope = {
		{ "k", 0.2, 0.1, 0.1, 0.4 },
		{ "k", 0.7, 0.1, 0.1, 0.4 },
		{ "k", 0.2, 0.46, 0.6, 0.1 },
		{ "d", 0.36, 0.58, 0.3, 0.3 },
	},
	-- Termometre: govde + hazne
	thermometer = {
		{ "k", 0.42, 0.06, 0.16, 0.6 },
		{ "d", 0.32, 0.58, 0.36, 0.36 },
	},
	-- Otoskop: koni uc + sap
	otoscope = {
		{ "k", 0.3, 0.08, 0.4, 0.24 },
		{ "k", 0.42, 0.3, 0.16, 0.6, -20 },
	},
	-- Rontgen: cerceve + icinde kemik izlenimi
	xray = {
		{ "k", 0.08, 0.1, 0.84, 0.8, 0, 0.65 },
		{ "k", 0.44, 0.24, 0.12, 0.52 },
		{ "d", 0.32, 0.16, 0.16, 0.16 },
		{ "d", 0.52, 0.68, 0.16, 0.16 },
	},
	-- Kan testi: tup + icindeki seviye
	bloodtest = {
		{ "k", 0.34, 0.06, 0.32, 0.86, 0, 0.6 },
		{ "k", 0.34, 0.46, 0.32, 0.46 },
	},
	-- Idrar tahlili: kap + kapak
	urinalysis = {
		{ "k", 0.28, 0.26, 0.44, 0.64 },
		{ "k", 0.22, 0.1, 0.56, 0.16 },
	},
	-- Ultrason: prob + dalgalar
	ultrasound = {
		{ "k", 0.1, 0.42, 0.3, 0.16, -20 },
		{ "d", 0.46, 0.16, 0.5, 0.5, 0, 0.6 },
		{ "d", 0.56, 0.34, 0.3, 0.3, 0, 0.35 },
	},
	-- ── Tedavi ikonlari (adlari tedavi kimlikleriyle ayni) ────────────
	-- Igne: govde + piston + ince uc
	injection = {
		{ "k", 0.3, 0.34, 0.4, 0.32, -45 },
		{ "k", 0.12, 0.12, 0.26, 0.12, -45 },
		{ "k", 0.62, 0.62, 0.3, 0.08, -45 },
	},
	-- Ilac: kapsul (iki yariya bolunmus yuvarlak dikdortgen)
	pill = {
		{ "d", 0.1, 0.34, 0.8, 0.32, -35 },
		{ "k", 0.46, 0.28, 0.08, 0.44, -35, 0.45 },
	},
	-- Sargi: capraz bant + iki dugum
	bandage = {
		{ "k", 0.1, 0.42, 0.8, 0.16, -30 },
		{ "k", 0.42, 0.42, 0.16, 0.16, 0, 0.4 },
	},
	-- Damla: damla govdesi + ucu
	drops = {
		{ "d", 0.28, 0.4, 0.44, 0.44 },
		{ "k", 0.38, 0.12, 0.24, 0.34, 0, 0.15 },
	},
	-- Yikama: kuvet + kopukler
	wash = {
		{ "k", 0.1, 0.5, 0.8, 0.34 },
		{ "d", 0.2, 0.22, 0.2, 0.2, 0, 0.35 },
		{ "d", 0.44, 0.12, 0.24, 0.24, 0, 0.2 },
		{ "d", 0.68, 0.26, 0.16, 0.16, 0, 0.45 },
	},
	-- Besleme: mama kabi + taneler
	feed = {
		{ "k", 0.12, 0.52, 0.76, 0.3 },
		{ "d", 0.26, 0.34, 0.18, 0.18 },
		{ "d", 0.46, 0.28, 0.18, 0.18 },
		{ "d", 0.62, 0.4, 0.16, 0.16 },
	},
	-- Su: damla + dalga
	water = {
		{ "d", 0.3, 0.34, 0.4, 0.4 },
		{ "k", 0.4, 0.1, 0.2, 0.3, 0, 0.15 },
		{ "k", 0.12, 0.82, 0.76, 0.1, 0, 0.4 },
	},
	-- Ameliyat: nester (sap + agiz)
	surgery = {
		{ "k", 0.14, 0.62, 0.42, 0.14, -35 },
		{ "k", 0.48, 0.2, 0.34, 0.26, -35 },
	},
	-- Gunduz: gunes
	day = {
		{ "d", 0.26, 0.26, 0.48, 0.48 },
		{ "k", 0.45, 0.02, 0.1, 0.16 },
		{ "k", 0.45, 0.82, 0.1, 0.16 },
		{ "k", 0.02, 0.45, 0.16, 0.1 },
		{ "k", 0.82, 0.45, 0.16, 0.1 },
	},
}

export type IconSpec = {
	name: string,
	size: number,
	color: Color3?,
	position: UDim2?,
	anchor: Vector2?,
	parent: Instance?,
	layoutOrder: number?,
	zIndex: number?,
}

--- Dokusuz ikon. Bilinmeyen ad icin bos kare doner (sessizce kaybolmaz,
--- ekranda kenarlikli bir kutu goruntuler - eksigi fark edilebilir olsun).
function Theme.icon(spec: IconSpec): Frame
	local holder = Instance.new("Frame")
	holder.Name = "Ikon_" .. spec.name
	holder.Size = UDim2.fromOffset(spec.size, spec.size)
	holder.Position = spec.position or UDim2.fromOffset(0, 0)
	holder.AnchorPoint = spec.anchor or Vector2.new(0, 0)
	holder.BackgroundTransparency = 1
	holder.BorderSizePixel = 0
	holder.LayoutOrder = spec.layoutOrder or 0
	holder.ZIndex = spec.zIndex or 1
	holder.Parent = spec.parent

	local color = spec.color or Theme.color.text
	local shapes = ICONS[spec.name]
	if shapes == nil then
		local missing = Instance.new("Frame")
		missing.Size = UDim2.fromScale(1, 1)
		missing.BackgroundTransparency = 1
		missing.ZIndex = holder.ZIndex
		missing.Parent = holder
		Theme.stroke(missing, color, 1)
		Theme.corner(missing, 3)
		return holder
	end

	for index, shape in shapes do
		local part = Instance.new("Frame")
		part.Name = "P" .. index
		part.Size = UDim2.fromScale(shape[4], shape[5])
		part.Position = UDim2.fromScale(shape[2], shape[3])
		part.BackgroundColor3 = color
		part.BackgroundTransparency = shape[7] or 0
		part.BorderSizePixel = 0
		part.Rotation = shape[6] or 0
		part.ZIndex = holder.ZIndex
		part.Parent = holder
		if shape[1] == "d" then
			Theme.corner(part, math.max(2, math.floor(spec.size * 0.5)))
		else
			Theme.corner(part, 2)
		end
	end
	return holder
end

-- ── Dugme ─────────────────────────────────────────────────────────────
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
	radius: number?,
	bold: boolean?,
	zIndex: number?,
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
	button.TextSize = spec.textSize or Theme.size.small
	button.Font = if spec.bold == false then Theme.font else Theme.fontBold
	button.AutoButtonColor = false
	button.LayoutOrder = spec.layoutOrder or 0
	button.ZIndex = spec.zIndex or 1
	button.Parent = spec.parent
	Theme.corner(button, spec.radius or Theme.radius.sm)
	Theme.stroke(button, Theme.color.strokeSoft, 1)

	local base = button.BackgroundColor3
	-- Uzerine gelince renk ve olcek birlikte degisiyor: yalniz renk
	-- degisimi dokunmatikte ve hizli fare hareketinde fark edilmiyor.
	local scale = Instance.new("UIScale")
	scale.Parent = button

	button.MouseEnter:Connect(function()
		Theme.tween(button, { BackgroundColor3 = base:Lerp(Color3.new(1, 1, 1), 0.16) }, Theme.motion.quick)
		Theme.tween(scale, { Scale = 1.03 }, Theme.motion.quick)
	end)
	button.MouseLeave:Connect(function()
		Theme.tween(button, { BackgroundColor3 = base }, Theme.motion.quick)
		Theme.tween(scale, { Scale = 1 }, Theme.motion.quick)
	end)
	button.MouseButton1Down:Connect(function()
		Theme.tween(scale, { Scale = 0.97 }, Theme.motion.quick)
	end)
	button.MouseButton1Up:Connect(function()
		Theme.tween(scale, { Scale = 1.03 }, Theme.motion.quick)
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

--- Kucuk bilgi rozeti (fiyat, "SAHIP", "KILITLI" gibi).
function Theme.badge(parent: Instance, text: string, color: Color3, order: number?): Frame
	local badge = Theme.frame({
		name = "Rozet",
		size = UDim2.fromOffset(0, 20),
		color = color,
		transparency = 0.78,
		radius = Theme.radius.sm,
		parent = parent,
		layoutOrder = order or 0,
	})
	badge.AutomaticSize = Enum.AutomaticSize.X
	Theme.padding(badge, 0, Theme.space.sm)
	Theme.stroke(badge, color, 1)
	local label = Theme.label({
		text = text,
		size = UDim2.fromOffset(0, 20),
		textSize = Theme.size.micro,
		bold = true,
		color = color,
		parent = badge,
	})
	label.AutomaticSize = Enum.AutomaticSize.X
	return badge
end

--- Panelleri bolen ince cizgi.
function Theme.divider(parent: Instance, order: number?): Frame
	return Theme.frame({
		name = "Ayirac",
		size = UDim2.new(1, 0, 0, 1),
		color = Theme.color.strokeSoft,
		transparency = 0.4,
		radius = 0,
		parent = parent,
		layoutOrder = order or 0,
	})
end

-- ── Cubuklar ──────────────────────────────────────────────────────────
export type Bar = {
	track: Frame,
	fill: Frame,
	ghost: Frame,
	color: Color3,
	ratio: number,
}

export type BarOptions = {
	segments: number?,
	radius: number?,
	ghost: boolean?,
}

--- Ilerleme/saglik cubugu. `Theme.setBar` ile guncelleniyor.
--- `segments` verilirse cubuk bolmeli cizilir (goz bir bolmeyi bir
--- yuzdeden daha hizli okuyor).
function Theme.bar(parent: Instance, size: UDim2, position: UDim2, color: Color3, options: BarOptions?): Bar
	local radius = (options and options.radius) or Theme.radius.sm
	local track = Theme.frame({
		name = "Track",
		size = size,
		position = position,
		color = Theme.color.panelDeep,
		transparency = 0.15,
		radius = radius,
		parent = parent,
	})
	Theme.stroke(track, Theme.color.strokeSoft, 1)

	-- Hayalet dolgu: gercek dolgunun ARKASINDA yavasca peslinden geliyor,
	-- boylece dusen degerin ne kadar dustugu bir an gorunuyor.
	local ghost = Theme.frame({
		name = "Ghost",
		size = UDim2.fromScale(0, 1),
		color = color:Lerp(Theme.color.shadow, 0.45),
		transparency = 0.25,
		radius = radius,
		parent = track,
	})

	local fill = Theme.frame({
		name = "Fill",
		size = UDim2.fromScale(0, 1),
		color = color,
		transparency = 0,
		radius = radius,
		parent = track,
	})
	fill.ZIndex = 2
	Theme.gradient(fill, color:Lerp(Color3.new(1, 1, 1), 0.25), color)

	local segments = (options and options.segments) or 0
	if segments > 1 then
		local holder = Theme.frame({
			name = "Bolmeler",
			size = UDim2.fromScale(1, 1),
			transparency = 1,
			radius = 0,
			parent = track,
		})
		holder.ZIndex = 3
		for index = 1, segments - 1 do
			Theme.frame({
				name = "Bolme",
				size = UDim2.new(0, 1, 1, 0),
				position = UDim2.fromScale(index / segments, 0),
				color = Theme.color.panelDeep,
				transparency = 0.25,
				radius = 0,
				parent = holder,
			})
		end
	end

	return { track = track, fill = fill, ghost = ghost, color = color, ratio = 0 }
end

--- Cubugu AKARAK doldurur. `instant` ilk cizimde kullaniliyor: acilista
--- her cubugun sifirdan akmasi gosterisli degil, yavas hissettiriyor.
function Theme.setBar(bar: Bar, ratio: number, instant: boolean?)
	local clamped = math.clamp(ratio, 0, 1)
	bar.ratio = clamped
	if instant == true then
		bar.fill.Size = UDim2.fromScale(clamped, 1)
		bar.ghost.Size = UDim2.fromScale(clamped, 1)
		return
	end
	Theme.tween(bar.fill, { Size = UDim2.fromScale(clamped, 1) }, Theme.motion.normal)
	Theme.tween(bar.ghost, { Size = UDim2.fromScale(clamped, 1) }, Theme.motion.slow)
end

--- Cubugun rengini durum rengine cevirir (saglik dusunce kirmiziya).
function Theme.setBarColor(bar: Bar, color: Color3)
	if bar.color == color then
		return
	end
	bar.color = color
	Theme.tween(bar.fill, { BackgroundColor3 = color }, Theme.motion.normal)
	Theme.tween(bar.ghost, { BackgroundColor3 = color:Lerp(Theme.color.shadow, 0.45) }, Theme.motion.normal)
end

-- ── Geri sayim halkasi ────────────────────────────────────────────────
export type Ring = { root: Frame, dots: { Frame }, color: Color3 }

--- Dokusuz halka: cember uzerine dizilmis noktalar. `Theme.setRing`
--- orani kadarini yakiyor. (Image kullanan bir halka bu ortamda
--- dogrulanamazdi; nokta dizisi ayni bilgiyi asset'siz veriyor.)
function Theme.ring(parent: Instance, diameter: number, position: UDim2, color: Color3, count: number?): Ring
	local total = count or 24
	local root = Theme.frame({
		name = "Halka",
		size = UDim2.fromOffset(diameter, diameter),
		position = position,
		anchor = Vector2.new(0.5, 0.5),
		transparency = 1,
		radius = 0,
		parent = parent,
	})

	local dots: { Frame } = {}
	local dotSize = math.max(3, math.floor(diameter * 0.09))
	for index = 1, total do
		local angle = (index - 1) / total * math.pi * 2 - math.pi / 2
		local dot = Theme.frame({
			name = "Nokta" .. index,
			size = UDim2.fromOffset(dotSize, dotSize),
			position = UDim2.new(
				0.5,
				math.cos(angle) * diameter * 0.42,
				0.5,
				math.sin(angle) * diameter * 0.42
			),
			anchor = Vector2.new(0.5, 0.5),
			color = color,
			transparency = 0.8,
			radius = dotSize,
			parent = root,
		})
		table.insert(dots, dot)
	end
	return { root = root, dots = dots, color = color }
end

function Theme.setRing(ring: Ring, ratio: number)
	local lit = math.floor(math.clamp(ratio, 0, 1) * #ring.dots + 0.5)
	for index, dot in ring.dots do
		dot.BackgroundTransparency = if index <= lit then 0 else 0.82
	end
end

function Theme.setRingColor(ring: Ring, color: Color3)
	if ring.color == color then
		return
	end
	ring.color = color
	for _, dot in ring.dots do
		dot.BackgroundColor3 = color
	end
end

-- ── Sayan sayilar ─────────────────────────────────────────────────────
export type Counter = { label: TextLabel, set: (number, boolean?) -> (), value: () -> number }

--- Para/XP gibi sayilar anlik ziplamak yerine SAYARAK artiyor.
--- Ara degerleri tween'e NumberValue uzerinden yaptiriyoruz: kendi
--- dongumuzu yazmak hem kare bagimli olurdu hem de iptal edilemezdi.
function Theme.counter(label: TextLabel, format: (number) -> string): Counter
	local holder = Instance.new("NumberValue")
	holder.Name = "Sayac"
	holder.Value = 0
	holder.Parent = label

	local current: Tween? = nil
	holder.Changed:Connect(function(value: number)
		label.Text = format(value)
	end)

	local counter = {}
	counter.label = label
	counter.value = function(): number
		return holder.Value
	end
	counter.set = function(target: number, instant: boolean?)
		if current ~= nil then
			current:Cancel()
			current = nil
		end
		if instant == true or math.abs(target - holder.Value) < 1 then
			holder.Value = target
			label.Text = format(target)
			return
		end
		current = Theme.tween(holder, { Value = target }, Theme.motion.count, Enum.EasingStyle.Quad)
	end
	counter.set(0, true)
	return counter
end

-- ── Panel acilis/kapanis ──────────────────────────────────────────────
local FADE_CLASSES = {
	Frame = { "BackgroundTransparency" },
	TextLabel = { "BackgroundTransparency", "TextTransparency" },
	TextButton = { "BackgroundTransparency", "TextTransparency" },
	ScrollingFrame = { "BackgroundTransparency" },
	UIStroke = { "Transparency" },
}

local function scaleOf(root: GuiObject): UIScale
	local existing = root:FindFirstChildOfClass("UIScale")
	if existing ~= nil then
		return existing
	end
	local scale = Instance.new("UIScale")
	scale.Parent = root
	return scale
end

--- Paneli OLCEK + SAYDAMLIK ile actirir. Anlik gorunen panel, oyuncunun
--- gozunun nereye bakacagini bilmemesine sebep oluyor; 0.22 saniyelik
--- bir giris bakisi panele cekiyor.
function Theme.open(root: GuiObject)
	root.Visible = true
	local scale = scaleOf(root)
	scale.Scale = 0.94

	-- Her ogenin KENDI hedef saydamligi var; hepsini 0'a cekmek yanlis
	-- olurdu (arka plani saydam olan etiketler gorunur kutuya donerdi).
	for _, item in root:GetDescendants() do
		local properties = FADE_CLASSES[item.ClassName]
		if properties == nil then
			continue
		end
		for _, property in properties do
			local target = (item :: any)[property]
			if target >= 1 then
				continue
			end
			;(item :: any)[property] = 1
			Theme.tween(item, { [property] = target }, Theme.motion.normal)
		end
	end

	local target = root.BackgroundTransparency
	root.BackgroundTransparency = math.min(1, target + 0.35)
	Theme.tween(root, { BackgroundTransparency = target }, Theme.motion.normal)
	Theme.tween(scale, { Scale = 1 }, Theme.motion.normal, Enum.EasingStyle.Back)
end

function Theme.close(root: GuiObject)
	local scale = scaleOf(root)
	local tween = Theme.tween(scale, { Scale = 0.94 }, Theme.motion.quick)
	tween.Completed:Once(function()
		root.Visible = false
		scale.Scale = 1
	end)
end

--- Panel gorunurlugunu tek satirda cevirir; hangi yone gidecegine
--- kendisi karar veriyor.
function Theme.toggle(root: GuiObject): boolean
	if root.Visible then
		Theme.close(root)
		return false
	end
	Theme.open(root)
	return true
end

-- ── Durum renkleri ────────────────────────────────────────────────────
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

--- Yuksek = iyi olan degerler (saglik, memnuniyet).
function Theme.statusColor(ratio: number): Color3
	if ratio >= 0.6 then
		return Theme.color.good
	elseif ratio >= 0.3 then
		return Theme.color.warn
	end
	return Theme.color.bad
end

--- Yuksek = kotu olan degerler (stres, agri).
function Theme.riskColor(ratio: number): Color3
	if ratio >= 0.7 then
		return Theme.color.bad
	elseif ratio >= 0.4 then
		return Theme.color.warn
	end
	return Theme.color.good
end

-- ── Kaydirilabilir liste ──────────────────────────────────────────────
function Theme.scroller(parent: Instance, size: UDim2, position: UDim2): ScrollingFrame
	local scroller = Instance.new("ScrollingFrame")
	scroller.Size = size
	scroller.Position = position
	scroller.BackgroundTransparency = 1
	scroller.BorderSizePixel = 0
	scroller.ScrollBarThickness = 4
	scroller.ScrollBarImageColor3 = Theme.color.stroke
	scroller.ScrollBarImageTransparency = 0.3
	scroller.AutomaticCanvasSize = Enum.AutomaticSize.Y
	scroller.CanvasSize = UDim2.fromOffset(0, 0)
	scroller.Parent = parent
	return scroller
end

return Theme
