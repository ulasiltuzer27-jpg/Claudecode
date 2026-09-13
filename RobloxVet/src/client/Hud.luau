--!strict
-- Hud.luau
-- ========
-- Ust bilgi katmani: para/seviye/itibar kartlari, XP cubugu, kuyruk,
-- HEDEF BANDI, kazanc yazilari ve stres vinyeti.
--
-- Neden kart?
-- -----------
-- Eskiden uc sayi alt alta duz yazidi; oyuncu hangisinin ne oldugunu
-- okumak zorundaydi. Ikonlu ayri kartlar, bakis bir yere dustugunde
-- okumadan taniniyor.
--
-- Sayilar SAYARAK artiyor (`Theme.counter`): para 1200'den 1320'ye
-- ziplarsa oyuncu kazandigini fark etmiyor; 0.45 saniyede sayarsa
-- fark ediyor. Ayni sebeple kazanc ayrica ekranda yukselen bir yazi
-- olarak da gosteriliyor.

local RunService = game:GetService("RunService")
local Workspace = game:GetService("Workspace")

local ReplicatedStorage = game:GetService("ReplicatedStorage")
local Shared = ReplicatedStorage:WaitForChild("VetShared")
local Loc = require(Shared.Loc)

local Theme = require(script.Parent.Theme)

local Hud = {}

local widgets: any = nil
local firstDraw = true
local objectiveKey = ""
local stressPulse: Theme.Pulse? = nil

--- Bir istatistik karti: ikon + sayan sayi + altinda kucuk aciklama.
local function statCard(parent: Instance, order: number, iconName: string, color: Color3, width: number): any
	local card = Theme.card({
		name = "Kart_" .. iconName,
		size = UDim2.new(0, width, 1, 0),
		color = Theme.color.panelLight,
		radius = Theme.radius.md,
		parent = parent,
		layoutOrder = order,
	}, color:Lerp(Theme.color.strokeSoft, 0.55))

	Theme.icon({
		name = iconName,
		size = 16,
		color = color,
		position = UDim2.fromOffset(Theme.space.sm, Theme.space.sm),
		parent = card,
		zIndex = 2,
	})

	local value = Theme.label({
		name = "Deger",
		text = "",
		size = UDim2.new(1, -Theme.space.sm * 2, 0, 24),
		position = UDim2.fromOffset(Theme.space.sm, Theme.space.sm + 16),
		textSize = Theme.size.big,
		bold = true,
		color = color,
		parent = card,
		zIndex = 2,
	})

	local caption = Theme.label({
		name = "Aciklama",
		text = "",
		size = UDim2.new(1, -Theme.space.sm * 2, 0, 14),
		position = UDim2.new(0, Theme.space.sm, 1, -18),
		textSize = Theme.size.micro,
		color = Theme.color.textFaint,
		parent = card,
		zIndex = 2,
	})

	return { root = card, value = value, caption = caption }
end

--- Ekran kenarindaki turuncu vinyet. Dokusuz: dort kenara birer serit
--- koyup her birinin UIGradient saydamligini kenardan ice dogru
--- acikliyoruz. Stres arttikca seritler belirginlesiyor.
local function buildVignette(parent: ScreenGui): { Frame }
	local strips: { Frame } = {}
	local specs = {
		{ UDim2.new(1, 0, 0, 120), UDim2.fromScale(0, 0), Vector2.new(0, 0), 90 },
		{ UDim2.new(1, 0, 0, 140), UDim2.fromScale(0, 1), Vector2.new(0, 1), 270 },
		{ UDim2.new(0, 140, 1, 0), UDim2.fromScale(0, 0), Vector2.new(0, 0), 0 },
		{ UDim2.new(0, 140, 1, 0), UDim2.fromScale(1, 0), Vector2.new(1, 0), 180 },
	}
	for index, spec in specs do
		local strip = Theme.frame({
			name = "Vinyet" .. index,
			size = spec[1] :: UDim2,
			position = spec[2] :: UDim2,
			anchor = spec[3] :: Vector2,
			color = Theme.color.stress,
			transparency = 1,
			radius = 0,
			parent = parent,
			zIndex = 0,
		})
		local gradient = Instance.new("UIGradient")
		gradient.Rotation = spec[4] :: number
		gradient.Transparency = NumberSequence.new({
			NumberSequenceKeypoint.new(0, 0),
			NumberSequenceKeypoint.new(1, 1),
		})
		gradient.Parent = strip
		table.insert(strips, strip)
	end
	return strips
end

function Hud.create(parent: ScreenGui)
	local vignette = buildVignette(parent)

	-- ── Sol ust: istatistik kartlari ──────────────────────────────────
	local stats = Theme.frame({
		name = "Istatistikler",
		size = UDim2.fromOffset(306, 64),
		position = UDim2.fromOffset(Theme.space.lg, Theme.space.lg),
		transparency = 1,
		radius = 0,
		parent = parent,
	})
	local row = Theme.list(stats, Theme.space.sm, Enum.FillDirection.Horizontal)
	row.VerticalAlignment = Enum.VerticalAlignment.Top

	local money = statCard(stats, 1, "money", Theme.color.money, 128)
	local level = statCard(stats, 2, "level", Theme.color.accentGlow, 84)
	local rep = statCard(stats, 3, "reputation", Theme.color.rep, 78)

	-- ── XP cubugu ─────────────────────────────────────────────────────
	-- Kart iki ilerlemeyi birlikte tasiyor: ustte OYUNCUNUN XP'si,
	-- altta ORTAK klinik seviyesi. Ayri bir karta konsaydi ekranda yer
	-- yoktu (sag sutun hasta kartiyla dolu); yan yana durmalari ayrica
	-- dogru: ikisi de "ilerleme", biri kisisel biri binanin.
	local xpCard = Theme.card({
		name = "XpKart",
		size = UDim2.fromOffset(306, 78),
		position = UDim2.fromOffset(Theme.space.lg, Theme.space.lg + 72),
		parent = parent,
	})
	local xpText = Theme.label({
		name = "XpYazi",
		text = "",
		size = UDim2.new(1, -Theme.space.md * 2, 0, 14),
		position = UDim2.fromOffset(Theme.space.md, 5),
		textSize = Theme.size.micro,
		color = Theme.color.textDim,
		parent = xpCard,
		zIndex = 2,
	})
	local xpBar = Theme.bar(
		xpCard,
		UDim2.new(1, -Theme.space.md * 2, 0, 10),
		UDim2.fromOffset(Theme.space.md, 22),
		Theme.color.xp,
		{ segments = 10 }
	)
	xpBar.track.ZIndex = 2
	xpBar.fill.ZIndex = 3

	Theme.divider(xpCard, 0).Position = UDim2.new(0, Theme.space.md, 0, 38)

	local clinicIcon = Theme.icon({
		name = "clinic",
		size = 12,
		color = Theme.color.accentGlow,
		position = UDim2.fromOffset(Theme.space.md, 44),
		parent = xpCard,
		zIndex = 2,
	})
	local clinicLabel = Theme.label({
		name = "KlinikYazi",
		text = "",
		size = UDim2.new(1, -Theme.space.md * 2 - 18, 0, 14),
		position = UDim2.fromOffset(Theme.space.md + 18, 43),
		textSize = Theme.size.micro,
		bold = true,
		color = Theme.color.textDim,
		parent = xpCard,
		zIndex = 2,
	})
	local clinicBar = Theme.bar(
		xpCard,
		UDim2.new(1, -Theme.space.md * 2, 0, 8),
		UDim2.fromOffset(Theme.space.md, 62),
		Theme.color.accentGlow,
		{ segments = 6 }
	)
	clinicBar.track.ZIndex = 2
	clinicBar.fill.ZIndex = 3

	-- Kalici kayit kapaliysa bunu SOYLE. Sessizce bellekte oynatmak,
	-- oyuncunun ilerlemesini kaybettiginde sebebini bilememesi demek.
	local warning = Theme.card({
		name = "KayitUyarisi",
		size = UDim2.fromOffset(306, 26),
		position = UDim2.fromOffset(Theme.space.lg, Theme.space.lg + 158),
		color = Theme.color.panel,
		parent = parent,
		visible = false,
	}, Theme.color.warn)
	Theme.icon({
		name = "alert",
		size = 12,
		color = Theme.color.warn,
		position = UDim2.fromOffset(Theme.space.sm, 7),
		parent = warning,
		zIndex = 2,
	})
	Theme.label({
		name = "Yazi",
		text = Loc.get("hud.transient"),
		size = UDim2.new(1, -28, 1, 0),
		position = UDim2.fromOffset(26, 0),
		textSize = Theme.size.micro,
		bold = true,
		color = Theme.color.warn,
		parent = warning,
		zIndex = 2,
	})

	-- ── Sag ust: kuyruk ───────────────────────────────────────────────
	local queue = Theme.card({
		name = "Kuyruk",
		size = UDim2.fromOffset(228, 64),
		position = UDim2.new(1, -Theme.space.lg, 0, Theme.space.lg),
		anchor = Vector2.new(1, 0),
		parent = parent,
	})
	Theme.icon({
		name = "queue",
		size = 16,
		color = Theme.color.accent,
		position = UDim2.fromOffset(Theme.space.md, Theme.space.md),
		parent = queue,
		zIndex = 2,
	})
	local queueText = Theme.label({
		name = "Sayac",
		text = "",
		size = UDim2.new(1, -Theme.space.md * 2 - 24, 0, 20),
		position = UDim2.fromOffset(Theme.space.md + 24, Theme.space.sm + 2),
		textSize = Theme.size.title,
		bold = true,
		parent = queue,
		zIndex = 2,
	})
	Theme.label({
		name = "Ipucu",
		text = Loc.get("hud.hints"),
		size = UDim2.new(1, -Theme.space.md * 2, 0, 16),
		position = UDim2.new(0, Theme.space.md, 1, -22),
		textSize = Theme.size.micro,
		color = Theme.color.textFaint,
		parent = queue,
		zIndex = 2,
	})

	-- ── Ust orta: hedef bandi ─────────────────────────────────────────
	-- Oyuncu "simdi ne yapmaliyim?" sorusunu bir daha sormasin diye
	-- sunucunun durumu tek cumleye cevriliyor.
	local objective = Theme.card({
		name = "HedefBandi",
		size = UDim2.fromOffset(336, 34),
		position = UDim2.new(0.5, 0, 0, Theme.space.lg),
		anchor = Vector2.new(0.5, 0),
		color = Theme.color.panel,
		parent = parent,
	}, Theme.color.accentDark)
	local objectiveIcon = Theme.icon({
		name = "check",
		size = 14,
		color = Theme.color.accentGlow,
		position = UDim2.fromOffset(Theme.space.md, 10),
		parent = objective,
		zIndex = 2,
	})
	local objectiveText = Theme.label({
		name = "Yazi",
		text = "",
		size = UDim2.new(1, -Theme.space.md * 2 - 20, 1, 0),
		position = UDim2.fromOffset(Theme.space.md + 20, 0),
		textSize = Theme.size.small,
		bold = true,
		color = Theme.color.text,
		parent = objective,
		zIndex = 2,
	})

	-- ── Vardiya: saat ve gece nobeti rozeti ───────────────────────────
	-- Saat SUNUCUDAN geliyor (DayCycle). Istemci kendi saatini
	-- isletseydi bir oyuncuda gece digerinde gunduz olurdu ve ucret
	-- carpani geceye bagli oldugu icin bu bir oynanis hatasi olurdu.
	local shift = Theme.card({
		name = "Vardiya",
		size = UDim2.fromOffset(228, 44),
		position = UDim2.new(1, -Theme.space.lg, 0, Theme.space.lg + 72),
		anchor = Vector2.new(1, 0),
		parent = parent,
	})
	local shiftIcon = Theme.icon({
		name = "day",
		size = 16,
		color = Theme.color.warn,
		position = UDim2.fromOffset(Theme.space.md, 14),
		parent = shift,
		zIndex = 2,
	})
	local shiftClock = Theme.label({
		name = "Saat",
		text = "",
		size = UDim2.fromOffset(70, 22),
		position = UDim2.fromOffset(Theme.space.md + 24, 11),
		textSize = Theme.size.title,
		bold = true,
		parent = shift,
		zIndex = 2,
	})
	local shiftLabel = Theme.label({
		name = "Vardiya",
		text = "",
		size = UDim2.new(1, -110, 0, 20),
		position = UDim2.fromOffset(104, 12),
		textSize = Theme.size.micro,
		bold = true,
		align = Enum.TextXAlignment.Right,
		color = Theme.color.textDim,
		parent = shift,
		zIndex = 2,
	})

	-- ── Bulasma uyarisi ───────────────────────────────────────────────
	-- Yalnizca TESHISI KONMUS bulasici bir hasta karantina disindayken
	-- goruluyor. Teshis oncesi bir uyari, hastalik listesini 35'ten
	-- 6'ya indiren bir bilgi sizdirirdi; sunucu bayragi ancak
	-- teshisten sonra kaldiriyor.
	--
	-- Yeri ortada: sag ve sol sutunlar dolu, ve bir uyarinin zaten
	-- gozun dustugu yerde durmasi gerekiyor.
	local contagion = Theme.card({
		name = "BulasmaUyarisi",
		size = UDim2.fromOffset(276, 30),
		-- Acil bandi (Chart.luau) ekranin ust ortasinda 58..114
		-- arasinda duruyor; uyari onun ALTINDA.
		position = UDim2.new(0.5, 0, 0, 122),
		anchor = Vector2.new(0.5, 0),
		color = Theme.color.panel,
		parent = parent,
	}, Theme.color.bad)
	Theme.icon({
		name = "alert",
		size = 13,
		color = Theme.color.bad,
		position = UDim2.fromOffset(Theme.space.md, 8),
		parent = contagion,
		zIndex = 2,
	})
	local contagionText = Theme.label({
		name = "Yazi",
		text = Loc.get("hud.contagion"),
		size = UDim2.new(1, -Theme.space.md * 2 - 20, 1, 0),
		position = UDim2.fromOffset(Theme.space.md + 20, 0),
		textSize = Theme.size.micro,
		bold = true,
		color = Theme.color.bad,
		parent = contagion,
		zIndex = 2,
	})
	contagion.Visible = false

	-- ── Vardiya olayi seridi ──────────────────────────────────────────
	-- Salgin, yogun/sakin gun, denetim. Bulasma uyarisinin ALTINDA
	-- (ust orta sutun: hedef seridi 16..50, acil bandi 58..114,
	-- bulasma 122..152, olay 160..194).
	--
	-- Geri sayimi ISTEMCI ciziyor: sunucu bitis anini MUTLAK zaman
	-- olarak yolluyor (Workspace:GetServerTimeNow ile ayni saat), bu
	-- yuzden saniyede bir paket yollamaya gerek yok.
	local event = Theme.card({
		name = "VardiyaOlayi",
		size = UDim2.fromOffset(300, 34),
		position = UDim2.new(0.5, 0, 0, 160),
		anchor = Vector2.new(0.5, 0),
		color = Theme.color.panel,
		parent = parent,
	}, Theme.color.xp)
	local eventIcon = Theme.icon({
		name = "clock",
		size = 13,
		color = Theme.color.xp,
		position = UDim2.fromOffset(Theme.space.md, 10),
		parent = event,
		zIndex = 2,
	})
	local eventText = Theme.label({
		name = "Yazi",
		text = "",
		size = UDim2.new(1, -Theme.space.md * 2 - 20 - 44, 1, 0),
		position = UDim2.fromOffset(Theme.space.md + 20, 0),
		textSize = Theme.size.micro,
		bold = true,
		color = Theme.color.text,
		parent = event,
		zIndex = 2,
	})
	local eventClock = Theme.label({
		name = "Sayac",
		text = "",
		size = UDim2.fromOffset(44, 34),
		position = UDim2.new(1, -Theme.space.md, 0, 0),
		anchor = Vector2.new(1, 0),
		textSize = Theme.size.micro,
		bold = true,
		align = Enum.TextXAlignment.Right,
		color = Theme.color.xp,
		parent = event,
		zIndex = 2,
	})
	event.Visible = false

	-- ── Kazanc yazilari ───────────────────────────────────────────────
	local floaters = Theme.frame({
		name = "Kazanclar",
		-- ui:katman — saydam kapsayici: kazanc yazilari HUD'un
		-- UZERINDE yukseliyor, bir yuva kaplamiyor.
		size = UDim2.fromOffset(180, 160),
		position = UDim2.fromOffset(Theme.space.lg + 132, Theme.space.lg + 70),
		transparency = 1,
		radius = 0,
		parent = parent,
	})

	widgets = {
		money = Theme.counter(money.value, function(value: number): string
			return Loc.get("hud.money", string.format("%d", math.floor(value + 0.5)))
		end),
		moneyCaption = money.caption,
		level = Theme.counter(level.value, function(value: number): string
			return string.format("%d", math.floor(value + 0.5))
		end),
		levelCaption = level.caption,
		rep = Theme.counter(rep.value, function(value: number): string
			return string.format("%d", math.floor(value + 0.5))
		end),
		repCaption = rep.caption,
		repRoot = rep.root,
		xpBar = xpBar,
		xpCard = xpCard,
		xpText = xpText,
		warning = warning,
		queueText = queueText,
		objective = objective,
		objectiveIcon = objectiveIcon,
		objectiveText = objectiveText,
		floaters = floaters,
		vignette = vignette,
		shift = shift,
		shiftIcon = shiftIcon,
		shiftClock = shiftClock,
		shiftLabel = shiftLabel,
		shiftNight = false,
		clinic = clinic,
		clinicIcon = clinicIcon,
		clinicLabel = clinicLabel,
		clinicBar = clinicBar,
		clinicShown = -1,
		contagion = contagion,
		contagionText = contagionText,
		contagionPulse = nil,
		event = event,
		eventIcon = eventIcon,
		eventText = eventText,
		eventClock = eventClock,
		eventEndsAt = 0,
		eventShown = "",
	}

	-- Olay geri sayimi her karede yeniden yaziliyor. Sunucu yalnizca
	-- BITIS ANINI yolluyor (senkron sunucu saatiyle); saniyede bir
	-- paket yollamak ayni bilgiyi tekrar tekrar tasimak olurdu.
	RunService.RenderStepped:Connect(function()
		if widgets == nil or not widgets.event.Visible then
			return
		end
		local left = math.max(0, widgets.eventEndsAt - Workspace:GetServerTimeNow())
		widgets.eventClock.Text = ("%d:%02d"):format(math.floor(left / 60), math.floor(left % 60))
		if left <= 0 then
			widgets.event.Visible = false
			widgets.eventShown = ""
		end
	end)

	widgets.moneyCaption.Text = Loc.get("hud.capMoney")
	widgets.levelCaption.Text = Loc.get("hud.capLevel")
	widgets.repCaption.Text = Loc.get("hud.capReputation")
	objective.Visible = false
end

function Hud.update(state: any)
	if widgets == nil or state == nil or state.ready ~= true then
		return
	end

	widgets.money.set(state.money, firstDraw)
	widgets.level.set(state.level, firstDraw)
	widgets.rep.set(state.reputation, firstDraw)
	widgets.levelCaption.Text = Loc.get(state.titleKey)

	Theme.setBar(widgets.xpBar, state.progress, firstDraw)
	if state.level >= state.maxLevel then
		widgets.xpText.Text = Loc.get("hud.xpMax", state.xp)
	else
		widgets.xpText.Text = Loc.get("hud.xp", state.xp)
	end

	-- Itibar dususte kirmiziya donuyor ve 25'in altinda nabiz atiyor:
	-- oyuncu itibarini kaybettigini fark etmezse duzeltemez.
	local ratio = state.reputation / 100
	widgets.rep.label.TextColor3 = Theme.statusColor(ratio)
	if ratio < 0.25 and stressPulse == nil then
		stressPulse = Theme.pulse(widgets.repRoot, "BackgroundTransparency", 0.45)
	elseif ratio >= 0.25 and stressPulse ~= nil then
		stressPulse.stop()
		stressPulse = nil
	end

	widgets.warning.Visible = state.transient == true
	widgets.queueText.Text = Loc.get("hud.queue", state.queueWaiting, state.queueSlots)

	-- Vardiya olayi. Yeni bir olay basladiysa serit bir kez parliyor;
	-- bildirim kayabilir, serit kacmaz.
	local info = state.event
	if info == nil then
		widgets.event.Visible = false
		widgets.eventShown = ""
		widgets.eventEndsAt = 0
	else
		widgets.event.Visible = true
		widgets.eventEndsAt = info.endsAt
		if widgets.eventShown ~= info.id then
			widgets.eventShown = info.id
			widgets.eventText.Text = Loc.get("hud.eventLine", Loc.get(info.nameKey), Loc.get(info.descKey))
			Theme.flash(widgets.event, Theme.color.xp)
		end
	end

	-- Bulasma uyarisi: gorunurken nabiz atiyor. Sessiz bir kirmizi
	-- kutu, ekranin sagindaki kartlarin arasinda kaybolurdu.
	local risky = state.contagion == true
	if risky ~= widgets.contagion.Visible then
		widgets.contagion.Visible = risky
		if risky then
			widgets.contagionPulse = Theme.pulse(widgets.contagionText, "TextTransparency", 0.55)
		elseif widgets.contagionPulse ~= nil then
			widgets.contagionPulse.stop()
			widgets.contagionPulse = nil
		end
	end

	-- Ortak klinik seviyesi. `state.clinic` sunucudan geliyor ve HERKESTE
	-- ayni: online oyuncularin en yuksek klinik XP'si. Istemci burada
	-- hicbir sey HESAPLAMIYOR, yalnizca ciziyor.
	if state.clinic ~= nil then
		local info = state.clinic
		local tail = if info.level >= info.maxLevel
			then Loc.get("hud.clinicMax")
			else Loc.get("hud.clinicNext", math.floor(info.progress * 100 + 0.5))
		widgets.clinicLabel.Text = Loc.get(
			"hud.clinicLine",
			info.level,
			info.maxLevel,
			Loc.get(info.nameKey),
			tail
		)
		Theme.setBar(widgets.clinicBar, info.progress, firstDraw)

		-- Seviye ATLAYINCA kart bir kez parliyor. Duyuru bildirimi
		-- kayabilir; kartin kendisi kacmaz.
		if widgets.clinicShown >= 0 and info.level > widgets.clinicShown then
			Theme.flash(widgets.xpCard, Theme.color.accentGlow)
		end
		widgets.clinicShown = info.level
	end

	if state.shift ~= nil then
		local hours = math.floor(state.shift.clockTime)
		local minutes = math.floor((state.shift.clockTime - hours) * 60)
		widgets.shiftClock.Text = ("%02d:%02d"):format(hours, minutes)

		local isNight = state.shift.night == true
		widgets.shiftLabel.Text = if isNight
			then Loc.get("hud.nightShift", ("%.2f"):format(state.shift.feeMultiplier))
			else Loc.get("hud.dayShift")
		widgets.shiftLabel.TextColor3 = if isNight then Theme.color.money else Theme.color.textDim

		-- Ikon yalnizca vardiya DEGISINCE yeniden kuruluyor: her
		-- guncellemede Frame yaratip yok etmek bosuna is.
		if isNight ~= widgets.shiftNight then
			widgets.shiftNight = isNight
			widgets.shiftIcon:Destroy()
			widgets.shiftIcon = Theme.icon({
				name = if isNight then "night" else "day",
				size = 16,
				color = if isNight then Theme.color.xp else Theme.color.warn,
				position = UDim2.fromOffset(Theme.space.md, 14),
				parent = widgets.shift,
				zIndex = 2,
			})
		end
	end

	firstDraw = false
end

--- Hedef bandini gunceller. Ayni hedef tekrar gelirse animasyon
--- tekrarlanmiyor - yanip sonen bir bant dikkat dagitir.
function Hud.setObjective(key: string?)
	if widgets == nil then
		return
	end
	if key == nil or key == "" then
		if objectiveKey ~= "" then
			objectiveKey = ""
			Theme.close(widgets.objective)
		end
		return
	end
	if key == objectiveKey then
		return
	end
	objectiveKey = key
	widgets.objectiveText.Text = Loc.get(key)
	if widgets.objective.Visible then
		Theme.tween(widgets.objectiveIcon, { Rotation = widgets.objectiveIcon.Rotation + 180 }, Theme.motion.slow)
	else
		Theme.open(widgets.objective)
	end
end

--- "+120 TL" yazisi: ekranda yukselip soluyor.
function Hud.gain(amount: number, kind: string?)
	if widgets == nil or amount == 0 then
		return
	end
	local color = if kind == "xp" then Theme.color.xp else Theme.color.money
	local text = if kind == "xp"
		then Loc.get("hud.gainXp", amount)
		else Loc.get("hud.gainMoney", amount)

	local label = Theme.label({
		name = "Kazanc",
		text = text,
		size = UDim2.new(1, 0, 0, 22),
		position = UDim2.fromOffset(0, 40),
		textSize = Theme.size.title,
		bold = true,
		color = color,
		parent = widgets.floaters,
	})
	label.TextStrokeTransparency = 0.5
	label.TextTransparency = 1

	Theme.tween(label, { TextTransparency = 0 }, Theme.motion.quick)
	Theme.tween(label, { Position = UDim2.fromOffset(0, -12) }, 1.1, Enum.EasingStyle.Quint)
	task.delay(0.55, function()
		if label.Parent == nil then
			return
		end
		local fade = Theme.tween(label, { TextTransparency = 1, TextStrokeTransparency = 1 }, 0.55)
		fade.Completed:Once(function()
			label:Destroy()
		end)
	end)
end

--- Stres vinyeti. 0.5'in altinda hic gorunmuyor: her hastada turuncu
--- bir cerceve, uyari olmaktan cikip arka plana donerdi.
function Hud.setStress(ratio: number)
	if widgets == nil then
		return
	end
	local strength = math.clamp((ratio - 0.5) / 0.5, 0, 1)
	local transparency = 1 - strength * 0.55
	for _, strip in widgets.vignette do
		Theme.tween(strip, { BackgroundTransparency = transparency }, Theme.motion.slow)
	end
end

return Hud
