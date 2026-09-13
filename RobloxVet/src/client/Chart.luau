--!strict
-- Chart.luau
-- ==========
-- Hasta karti: tur, sahibinin sikayeti, saglik/stres/memnuniyet ve
-- BULUNMUS belirtiler. Ayrica acil vakalarin geri sayimi.
--
-- Kartta hastaligin adi ancak dogru teshisten SONRA cikiyor - cunku
-- sunucu oncesinde onu hic yollamiyor (PatientFlow.chartSnapshot).
--
-- Sahibin sikayeti neden burada?
-- ------------------------------
-- NPC'nin basindaki balon uzaktan okunmuyor ve oyuncu masaya egildiginde
-- kayboluyor. Ayni cumle kartta da duruyor: ipucu, isin yapildigi yerde
-- goz onunde olmali.
--
-- Vitaller neden BOLMELI?
-- -----------------------
-- Duz bir cubugun %62 mi %71 mi oldugu okunmuyor; on bolmeli cubukta
-- "yedi bolme dolu" bir bakista goruluyor. Renk de esige gore degisiyor
-- (Theme.statusColor), yani dusen saglik once sariya sonra kirmiziya
-- donuyor - sayiyi okumaya gerek kalmadan.

local ReplicatedStorage = game:GetService("ReplicatedStorage")
local Shared = ReplicatedStorage:WaitForChild("VetShared")
local Loc = require(Shared.Loc)
local Net = require(Shared.Net)

local Theme = require(script.Parent.Theme)

local Chart = {}

local widgets: any = nil
local emergencyPulse: Theme.Pulse? = nil

local SEVERITY_COLOR = {
	[1] = Theme.color.textDim,
	[2] = Theme.color.warn,
	[3] = Theme.color.bad,
}

--- Tek bir vital satiri: ad + sayi + bolmeli cubuk.
local function meter(parent: Instance, name: string, y: number, color: Color3): any
	local caption = Theme.label({
		name = name .. "Ad",
		text = "",
		size = UDim2.new(0.6, 0, 0, 14),
		position = UDim2.fromOffset(Theme.space.md, y),
		textSize = Theme.size.micro,
		color = Theme.color.textDim,
		parent = parent,
		zIndex = 2,
	})
	local value = Theme.label({
		name = name .. "Deger",
		text = "",
		size = UDim2.new(0.4, -Theme.space.md, 0, 14),
		position = UDim2.new(0.6, 0, 0, y),
		textSize = Theme.size.micro,
		bold = true,
		align = Enum.TextXAlignment.Right,
		color = color,
		parent = parent,
		zIndex = 2,
	})
	local bar = Theme.bar(
		parent,
		UDim2.new(1, -Theme.space.md * 2, 0, 9),
		UDim2.fromOffset(Theme.space.md, y + 16),
		color,
		{ segments = 10 }
	)
	bar.track.ZIndex = 2
	bar.fill.ZIndex = 3
	return { caption = caption, value = value, bar = bar }
end

function Chart.create(parent: ScreenGui)
	-- Acil vaka bandi ekranin ORTASINDA ve ustte, hedef bandinin hemen
	-- altinda: geri sayim, gozun kacinilmaz olarak dustugu yerde olmali.
	local banner = Theme.card({
		name = "AcilBandi",
		size = UDim2.fromOffset(300, 56),
		position = UDim2.new(0.5, 0, 0, 58),
		anchor = Vector2.new(0.5, 0),
		color = Color3.fromRGB(74, 22, 24),
		parent = parent,
		visible = false,
	}, Theme.color.bad)

	local ring = Theme.ring(banner, 40, UDim2.new(0, 34, 0.5, 0), Theme.color.bad, 20)
	ring.root.ZIndex = 3
	for _, dot in ring.dots do
		dot.ZIndex = 3
	end

	local bannerText = Theme.label({
		name = "Metin",
		text = "",
		size = UDim2.new(1, -72, 1, 0),
		position = UDim2.fromOffset(62, 0),
		textSize = Theme.size.title,
		bold = true,
		align = Enum.TextXAlignment.Left,
		color = Theme.color.text,
		parent = banner,
		zIndex = 2,
	})

	-- ── Hasta karti ───────────────────────────────────────────────────
	local panel = Theme.card({
		name = "HastaKarti",
		-- Sag sutun asagidan yukariya: alet cubugu, teshis/tedavi paneli,
		-- hasta karti, vardiya karti, kuyruk karti. Yukseklikler bu
		-- sirayla toplaniyor; biri degisirse asagidakiler de kayar.
		size = UDim2.fromOffset(330, 344),
		position = UDim2.new(1, -Theme.space.lg, 0, 144),
		anchor = Vector2.new(1, 0),
		parent = parent,
	})

	Theme.icon({
		name = "paw",
		size = 18,
		color = Theme.color.accent,
		position = UDim2.fromOffset(Theme.space.md, Theme.space.md),
		parent = panel,
		zIndex = 2,
	})

	local title = Theme.label({
		name = "Baslik",
		text = Loc.get("chart.empty"),
		size = UDim2.new(1, -Theme.space.md * 2 - 26, 0, 24),
		position = UDim2.fromOffset(Theme.space.md + 26, Theme.space.sm + 2),
		textSize = Theme.size.title,
		bold = true,
		parent = panel,
		zIndex = 2,
	})

	local condition = Theme.label({
		name = "Hastalik",
		text = "",
		size = UDim2.new(1, -Theme.space.md * 2, 0, 18),
		position = UDim2.fromOffset(Theme.space.md, 36),
		textSize = Theme.size.small,
		bold = true,
		color = Theme.color.good,
		parent = panel,
		zIndex = 2,
	})

	-- Sahibin sikayeti ALINTI BALONU icinde: kartin geri kalani olcum,
	-- bu ise bir insanin soyledigi sey. Gorsel olarak ayrilmasi, oyuncunun
	-- onu "veri" degil "ipucu" diye okumasini sagliyor.
	local quote = Theme.frame({
		name = "SikayetBalonu",
		size = UDim2.new(1, -Theme.space.md * 2, 0, 44),
		position = UDim2.fromOffset(Theme.space.md, 58),
		color = Theme.color.panelDeep,
		transparency = 0.25,
		radius = Theme.radius.sm,
		parent = panel,
	})
	quote.ZIndex = 2
	Theme.frame({
		name = "Serit",
		size = UDim2.new(0, 3, 1, -10),
		position = UDim2.fromOffset(6, 5),
		color = Theme.color.accent,
		transparency = 0.2,
		radius = 2,
		parent = quote,
		zIndex = 3,
	})
	local hint = Theme.label({
		name = "Sikayet",
		text = "",
		size = UDim2.new(1, -22, 1, -6),
		position = UDim2.fromOffset(16, 3),
		textSize = Theme.size.micro,
		color = Theme.color.textDim,
		wrapped = true,
		parent = quote,
		zIndex = 3,
	})

	local health = meter(panel, "Saglik", 114, Theme.color.health)
	local stress = meter(panel, "Stres", 150, Theme.color.stress)
	local satisfaction = meter(panel, "Memnuniyet", 186, Theme.color.accent)

	Theme.divider(panel, 0).Position = UDim2.new(0, Theme.space.md, 0, 220)

	Theme.label({
		name = "SemptomBaslik",
		text = Loc.get("chart.symptoms"),
		size = UDim2.new(1, -Theme.space.md * 2, 0, 16),
		position = UDim2.fromOffset(Theme.space.md, 228),
		textSize = Theme.size.micro,
		bold = true,
		color = Theme.color.textFaint,
		parent = panel,
		zIndex = 2,
	})

	local list = Theme.scroller(
		panel,
		UDim2.new(1, -Theme.space.md * 2, 0, 64),
		UDim2.fromOffset(Theme.space.md, 248)
	)
	list.ZIndex = 2
	Theme.list(list, Theme.space.xs)

	local discharge = Theme.button({
		name = "Taburcu",
		text = Loc.get("chart.discharge"),
		size = UDim2.new(1, -Theme.space.md * 2, 0, 32),
		position = UDim2.new(0, Theme.space.md, 1, -42),
		color = Theme.color.accentDark,
		textSize = Theme.size.small,
		zIndex = 2,
		parent = panel,
		onClick = function()
			Net.toServer("Discharge")
		end,
	})

	widgets = {
		panel = panel,
		banner = banner,
		bannerText = bannerText,
		ring = ring,
		title = title,
		condition = condition,
		hint = hint,
		quote = quote,
		health = health,
		stress = stress,
		satisfaction = satisfaction,
		list = list,
		discharge = discharge,
		listKey = "",
	}
end

--- Bir vital satirini gunceller; rengi esige gore degistiriyor.
local function setMeter(entry: any, key: string, value: number, risky: boolean)
	local ratio = value / 100
	local color = if risky then Theme.riskColor(ratio) else Theme.statusColor(ratio)
	entry.caption.Text = Loc.get(key)
	entry.value.Text = string.format("%d", value)
	entry.value.TextColor3 = color
	Theme.setBar(entry.bar, ratio)
	Theme.setBarColor(entry.bar, color)
end

function Chart.update(chart: any)
	if widgets == nil then
		return
	end

	if chart == nil or chart.present ~= true then
		widgets.title.Text = Loc.get("chart.empty")
		widgets.condition.Text = ""
		widgets.hint.Text = ""
		widgets.quote.Visible = false
		for _, entry in { widgets.health, widgets.stress, widgets.satisfaction } do
			entry.caption.Text = ""
			entry.value.Text = ""
			Theme.setBar(entry.bar, 0)
		end
		widgets.list:ClearAllChildren()
		Theme.list(widgets.list, Theme.space.xs)
		widgets.listKey = ""
		widgets.discharge.Visible = false
		widgets.banner.Visible = false
		if emergencyPulse ~= nil then
			emergencyPulse.stop()
			emergencyPulse = nil
		end
		return
	end

	widgets.title.Text = Loc.get(chart.nameKey)
	-- Rutin islemde "Teshis: Asi" yazmak yaniltici olurdu: teshis diye
	-- bir sey yok, hasta zaten asi olmaya geldi.
	if chart.routine == true then
		widgets.condition.Text = Loc.get("chart.routine")
		widgets.condition.TextColor3 = Theme.color.accentGlow
	else
		widgets.condition.Text = if chart.diagnosed and chart.conditionNameKey ~= nil
			then Loc.get("chart.diagnosed", Loc.get(chart.conditionNameKey))
			else Loc.get("chart.undiagnosed")
		widgets.condition.TextColor3 = if chart.diagnosed then Theme.color.good else Theme.color.warn

		-- BULASICILIK teshisten sonra karta yaziliyor. Karantinaya
		-- alinmissa yesil, alinmamissa kirmizi: "yapilacak is var mi"
		-- sorusunun cevabi tek bakista gorunsun.
		if chart.contagious == true then
			if chart.quarantined == true then
				widgets.condition.Text = widgets.condition.Text
					.. " \u{00B7} " .. Loc.get("chart.contagious")
					.. " (" .. Loc.get("chart.quarantined") .. ")"
				widgets.condition.TextColor3 = Theme.color.good
			else
				widgets.condition.Text = widgets.condition.Text
					.. " \u{00B7} " .. Loc.get("chart.contagious")
				widgets.condition.TextColor3 = Theme.color.bad
			end
		end
	end

	widgets.quote.Visible = chart.hintKey ~= nil
	widgets.hint.Text = if chart.hintKey ~= nil
		then "\u{201C}" .. Loc.get(chart.hintKey) .. "\u{201D}"
		else ""

	setMeter(widgets.health, "chart.healthShort", chart.health, false)
	setMeter(widgets.stress, "chart.stressShort", chart.stress, true)
	setMeter(widgets.satisfaction, "chart.satisfactionShort", chart.satisfaction, false)

	-- Acil vaka geri sayimi. secondsLeft negatifse acil degil.
	if chart.emergency == true and chart.secondsLeft >= 0 then
		local seconds = math.floor(chart.secondsLeft)
		widgets.banner.Visible = true
		widgets.bannerText.Text = Loc.get("chart.emergency", math.floor(seconds / 60), seconds % 60)
		-- Halka kalan sureyi gosteriyor; 90 saniye tam tur kabul ediliyor.
		Theme.setRing(widgets.ring, math.clamp(chart.secondsLeft / 90, 0, 1))
		local critical = chart.secondsLeft < 20
		Theme.setRingColor(widgets.ring, if critical then Theme.color.bad else Theme.color.warn)
		if critical and emergencyPulse == nil then
			emergencyPulse = Theme.pulse(widgets.banner, "BackgroundTransparency", 0.5, 0.45)
		elseif not critical and emergencyPulse ~= nil then
			emergencyPulse.stop()
			emergencyPulse = nil
		end
	else
		widgets.banner.Visible = false
		if emergencyPulse ~= nil then
			emergencyPulse.stop()
			emergencyPulse = nil
		end
	end

	-- Belirti listesi her karede yeniden kurulmuyor: sayi degismediyse
	-- dokunulmuyor. (Aksi halde kaydirma cubugu her guncellemede basa
	-- donerdi ve liste titrerdi.)
	local samples = chart.samples or {}
	local listKey = ("%d/%d/%d/%s"):format(
		#chart.revealed,
		#samples,
		chart.analyzed or 0,
		tostring(chart.returning == true) .. tostring(chart.returningKey)
	)
	if widgets.listKey ~= listKey then
		widgets.listKey = listKey
		widgets.list:ClearAllChildren()
		Theme.list(widgets.list, Theme.space.xs)

		-- KRONIK DONUS satiri en ustte: hastanin kim oldugu, ne
		-- bulundugundan once gelir.
		--
		-- Hastaligin adini YALNIZCA o vakayi kendi gecmisinde tutan
		-- oyuncu goruyor (sunucu `returningKey`i yalnizca ona
		-- dolduruyor). Baska bir veteriner "bu hayvan daha once
		-- buradaydi" satirini goruyor ama neyle geldigini bilmiyor -
		-- kayit tutmanin karsiligi bu.
		if chart.returning == true then
			local row = Theme.frame({
				name = "Gecmis",
				size = UDim2.new(1, -Theme.space.sm, 0, 24),
				color = Theme.color.panelLight,
				transparency = 0.2,
				radius = Theme.radius.sm,
				parent = widgets.list,
				layoutOrder = -200,
				zIndex = 2,
			})
			Theme.icon({
				name = "clock",
				size = 10,
				color = Theme.color.accentGlow,
				position = UDim2.new(0, Theme.space.sm, 0.5, -5),
				parent = row,
				zIndex = 3,
			})
			local days = chart.returningDays or 0
			local text
			if chart.returningKey ~= nil then
				text = if days <= 0
					then Loc.get("chart.returningToday")
					else Loc.get("chart.returningKnown", days, Loc.get(chart.returningKey))
			else
				text = Loc.get("chart.returningOther")
			end
			Theme.label({
				text = text,
				size = UDim2.new(1, -26, 1, 0),
				position = UDim2.fromOffset(22, 0),
				textSize = Theme.size.micro,
				bold = true,
				color = Theme.color.accentGlow,
				parent = row,
				zIndex = 3,
			})
		end

		-- Bekleyen numuneler EN USTTE: yapilacak is, yapilmis isin
		-- ustunde durmali.
		for index, sample in samples do
			local row = Theme.frame({
				name = "Numune",
				size = UDim2.new(1, -Theme.space.sm, 0, 24),
				color = Theme.color.panelLight,
				transparency = 0.2,
				radius = Theme.radius.sm,
				parent = widgets.list,
				layoutOrder = -100 + index,
				zIndex = 2,
			})
			Theme.icon({
				name = "bloodtest",
				size = 10,
				color = Theme.color.warn,
				position = UDim2.new(0, Theme.space.sm, 0.5, -5),
				parent = row,
				zIndex = 3,
			})
			Theme.label({
				text = Loc.get("chart.samplePending", Loc.get(sample.nameKey)),
				size = UDim2.new(1, -26, 1, 0),
				position = UDim2.fromOffset(22, 0),
				textSize = Theme.size.micro,
				bold = true,
				color = Theme.color.warn,
				parent = row,
				zIndex = 3,
			})
		end

		if #chart.revealed == 0 and #samples == 0 and chart.returning ~= true then
			Theme.label({
				text = Loc.get("chart.noSymptoms"),
				size = UDim2.new(1, -Theme.space.sm, 0, 20),
				textSize = Theme.size.micro,
				color = Theme.color.textFaint,
				parent = widgets.list,
				zIndex = 2,
			})
		else
			for index, symptom in chart.revealed do
				local color = SEVERITY_COLOR[symptom.severity] or Theme.color.text
				local row = Theme.frame({
					name = "Belirti",
					size = UDim2.new(1, -Theme.space.sm, 0, 24),
					color = Theme.color.panelLight,
					transparency = 0.35,
					radius = Theme.radius.sm,
					parent = widgets.list,
					layoutOrder = index,
					zIndex = 2,
				})
				Theme.frame({
					name = "Siddet",
					size = UDim2.fromOffset(6, 6),
					position = UDim2.new(0, Theme.space.sm, 0.5, -3),
					color = color,
					transparency = 0,
					radius = 3,
					parent = row,
					zIndex = 3,
				})
				Theme.label({
					text = Loc.get(symptom.nameKey),
					size = UDim2.new(1, -26, 1, 0),
					position = UDim2.fromOffset(22, 0),
					textSize = Theme.size.small,
					color = color,
					parent = row,
					zIndex = 3,
				})
			end
		end
	end

	widgets.discharge.Visible = chart.complete == true
end

return Chart
