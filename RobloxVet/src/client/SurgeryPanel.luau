--!strict
-- SurgeryPanel.luau
-- =================
-- Ameliyat mini-oyununun arayuzu. YALNIZCA CIZER: puanlama sunucuda.
--
-- Zamanlama
-- ---------
-- Sunucu adimlarin hedef zamanini SENKRON sunucu saatine gore yolluyor
-- (`workspace:GetServerTimeNow()`). Istemci ayni saati okudugu icin
-- isaretci sunucunun bekledigi anda tam ortadan geciyor; kendi
-- saatimizi kullansak gecikme kadar kayardi.

local RunService = game:GetService("RunService")
local UserInputService = game:GetService("UserInputService")
local Workspace = game:GetService("Workspace")

local ReplicatedStorage = game:GetService("ReplicatedStorage")
local Shared = ReplicatedStorage:WaitForChild("VetShared")
local Audio = require(Shared.Audio)
local Loc = require(Shared.Loc)
local Net = require(Shared.Net)

local Theme = require(script.Parent.Theme)

local SurgeryPanel = {}

local widgets: any = nil
local schedule: any = nil
local connection: RBXScriptConnection? = nil
local sentSteps: { [number]: boolean } = {}
-- Sunucunun notu hangi adima ait oldugunu soylemiyor (tek bir dize
-- geliyor); en son gonderilen adimi burada tutup noktayi ona gore
-- boyuyoruz. Adimlar sirayla gittigi icin bu esleme guvenli.
local lastPressed = 0

-- Asistanlik durumu: sunucudan geliyor, istemci yalnizca ciziyor.
local assistState: any = { active = false, canAssist = false, assisting = false }
local assistHeld = false

local TRACK_WIDTH = 440

--- Isaretcinin ray uzerindeki yeri: ZAMANI konuma ceviren fonksiyon.
---
--- Neden ayni fonksiyon bolgeyi de ciziyor?
--- Sunucu kararini yalnizca ZAMANA gore veriyor (|simdi - hedef|).
--- Isaretciyi egri bir fonksiyonla cizip yesil bolgeyi duz cizseydik
--- "bolgedeyim" ile "zamanindayim" ayni sey olmaktan cikardi - yani
--- ekran yalan soylerdi. Bu yuzden hem isaretci hem bolgenin KENARLARI
--- ayni fonksiyondan geciyor ve fonksiyon ARTAN olmak zorunda: geri
--- giden bir isaretci bolgeye birden fazla kez girerdi.
local function placeOnTrack(motion: string, ratio: number): number
	local p = math.clamp(ratio, 0, 1)
	if motion == "ease" then
		-- Kenarlarda yavas, ortada hizli: hizalama anini daraltiyor.
		return p * p * (3 - 2 * p)
	elseif motion == "pulse" then
		-- Nabiz gibi duraklayip ilerliyor. Genlik bilerek kucuk:
		-- 0.06 * 4pi = 0.75 < 1, yani turev hep pozitif (artan).
		return math.clamp(p + 0.06 * math.sin(p * math.pi * 4), 0, 1)
	end
	return p
end

local function currentStep(now: number): number
	if schedule == nil then
		return 0
	end
	local raw = (now - schedule.startTime) / schedule.stepSeconds
	return math.clamp(math.floor(raw + 0.5), 1, schedule.steps)
end

local function stop()
	if connection ~= nil then
		connection:Disconnect()
		connection = nil
	end
	schedule = nil
	lastPressed = 0
	table.clear(sentSteps)
	if widgets ~= nil and widgets.panel.Visible then
		Theme.close(widgets.panel)
	end
end

local function press()
	if schedule == nil then
		return
	end
	local step = currentStep(Workspace:GetServerTimeNow())
	if step < 1 or sentSteps[step] then
		return
	end
	sentSteps[step] = true
	lastPressed = step
	Net.toServer("SurgeryInput", step)
end

function SurgeryPanel.create(parent: ScreenGui)
	local panel = Theme.card({
		name = "AmeliyatPaneli",
		size = UDim2.fromOffset(TRACK_WIDTH + 56, 196),
		position = UDim2.new(0.5, 0, 0.5, 0),
		anchor = Vector2.new(0.5, 0.5),
		parent = parent,
		visible = false,
	}, Theme.color.accent)

	Theme.icon({
		name = "surgery",
		size = 18,
		color = Theme.color.accentGlow,
		position = UDim2.new(0.5, -78, 0, 12),
		parent = panel,
		zIndex = 2,
	})
	local title = Theme.label({
		name = "Baslik",
		text = Loc.get("surgery.title"),
		size = UDim2.new(1, -Theme.space.lg * 2, 0, 24),
		position = UDim2.fromOffset(Theme.space.lg, 10),
		textSize = Theme.size.title,
		bold = true,
		align = Enum.TextXAlignment.Center,
		parent = panel,
		zIndex = 2,
	})
	local hint = Theme.label({
		name = "Ipucu",
		text = Loc.get("surgery.hint"),
		size = UDim2.new(1, -Theme.space.lg * 2, 0, 18),
		position = UDim2.fromOffset(Theme.space.lg, 36),
		textSize = Theme.size.micro,
		color = Theme.color.textDim,
		align = Enum.TextXAlignment.Center,
		parent = panel,
		zIndex = 2,
	})

	local track = Theme.frame({
		name = "Ray",
		size = UDim2.fromOffset(TRACK_WIDTH, 38),
		position = UDim2.new(0.5, 0, 0, 68),
		anchor = Vector2.new(0.5, 0),
		color = Theme.color.panelDeep,
		transparency = 0,
		radius = Theme.radius.sm,
		parent = panel,
		zIndex = 2,
	})
	Theme.stroke(track, Theme.color.strokeSoft, 1)

	-- TIK ISARETLERI: ray boyunca esit arayla ince cizgiler. Isaretcinin
	-- ne kadar hizli gittigini ve merkeze ne kadar kaldigini bunlar
	-- okunur kiliyor; bos bir ray uzerinde hiz algilanmiyor.
	for index = 1, 9 do
		local major = index == 5
		Theme.frame({
			name = "Tik" .. index,
			size = UDim2.new(0, if major then 2 else 1, 0, if major then 38 else 12),
			position = UDim2.new(index / 10, 0, if major then 0 else 0.5, if major then 0 else -6),
			color = Theme.color.strokeSoft,
			transparency = if major then 0.1 else 0.45,
			radius = 0,
			parent = track,
			zIndex = 3,
		})
	end

	local goodZone = Theme.frame({
		name = "IyiBolge",
		size = UDim2.fromOffset(40, 38),
		position = UDim2.new(0.5, 0, 0, 0),
		anchor = Vector2.new(0.5, 0),
		color = Theme.color.accentDark,
		transparency = 0.3,
		radius = Theme.radius.sm,
		parent = track,
		zIndex = 4,
	})
	local perfectZone = Theme.frame({
		name = "TamBolge",
		size = UDim2.fromOffset(20, 38),
		position = UDim2.new(0.5, 0, 0, 0),
		anchor = Vector2.new(0.5, 0),
		color = Theme.color.good,
		transparency = 0.2,
		radius = Theme.radius.sm,
		parent = track,
		zIndex = 5,
	})
	-- Mukemmel bolge nabiz atiyor: ray uzerinde nereye basilacagi
	-- bir bakista belli olsun.
	Theme.pulse(perfectZone, "BackgroundTransparency", 0.5, 0.9)

	local marker = Theme.frame({
		name = "Isaretci",
		size = UDim2.fromOffset(6, 50),
		position = UDim2.new(0, 0, 0.5, 0),
		anchor = Vector2.new(0.5, 0.5),
		color = Theme.color.text,
		transparency = 0,
		radius = 3,
		parent = track,
		zIndex = 6,
	})
	marker.ZIndex = 6

	-- Adim noktalari: kac adim var, kacinci adimdayiz. Sayidan once
	-- goz bunu okuyor.
	local pipRow = Theme.frame({
		name = "AdimNoktalari",
		size = UDim2.new(1, -Theme.space.lg * 2, 0, 10),
		position = UDim2.fromOffset(Theme.space.lg, 116),
		transparency = 1,
		radius = 0,
		parent = panel,
		zIndex = 2,
	})
	local pipLayout = Theme.list(pipRow, Theme.space.xs, Enum.FillDirection.Horizontal)
	pipLayout.HorizontalAlignment = Enum.HorizontalAlignment.Center

	local steps = Theme.label({
		name = "Adim",
		text = "",
		size = UDim2.new(1, -Theme.space.lg * 2, 0, 20),
		position = UDim2.fromOffset(Theme.space.lg, 132),
		textSize = Theme.size.small,
		bold = true,
		align = Enum.TextXAlignment.Center,
		color = Theme.color.textDim,
		parent = panel,
		zIndex = 2,
	})
	local grade = Theme.label({
		name = "Not",
		text = "",
		size = UDim2.new(1, -Theme.space.lg * 2, 0, 26),
		position = UDim2.fromOffset(Theme.space.lg, 156),
		textSize = Theme.size.title,
		bold = true,
		align = Enum.TextXAlignment.Center,
		parent = panel,
		zIndex = 2,
	})
	local gradeScale = Instance.new("UIScale")
	gradeScale.Parent = grade

	local button = Theme.button({
		name = "Kes",
		text = "",
		size = UDim2.fromScale(1, 1),
		color = Theme.color.panel,
		parent = panel,
		onClick = press,
	})
	button.BackgroundTransparency = 1
	button.ZIndex = 0

	-- ── Asistan karti ─────────────────────────────────────────────────
	-- Ameliyathanedeki ikinci oyuncu icin. Cerrahin panelinden AYRI:
	-- ikisi ayni anda ekranda olabiliyor (cerrah kendi kararlilik
	-- cubugunu goruyor, asistan kendi cagrisini).
	local assistCard = Theme.card({
		name = "AsistanKarti",
		size = UDim2.fromOffset(340, 72),
		position = UDim2.new(0.5, 0, 1, -120),
		anchor = Vector2.new(0.5, 1),
		parent = parent,
		visible = false,
	}, Theme.color.accentGlow)

	Theme.icon({
		name = "health",
		size = 16,
		color = Theme.color.accentGlow,
		position = UDim2.fromOffset(Theme.space.md, 12),
		parent = assistCard,
		zIndex = 2,
	})
	local assistText = Theme.label({
		name = "Yazi",
		text = "",
		size = UDim2.new(1, -Theme.space.md * 2 - 24, 0, 18),
		position = UDim2.fromOffset(Theme.space.md + 24, 10),
		textSize = Theme.size.small,
		bold = true,
		parent = assistCard,
		zIndex = 2,
	})
	local assistHint = Theme.label({
		name = "Ipucu",
		text = Loc.get("surgery.assist.hint"),
		size = UDim2.new(1, -Theme.space.md * 2, 0, 14),
		position = UDim2.fromOffset(Theme.space.md, 28),
		textSize = Theme.size.micro,
		color = Theme.color.textDim,
		parent = assistCard,
		zIndex = 2,
	})
	local assistBar = Theme.bar(
		assistCard,
		UDim2.new(1, -Theme.space.md * 2, 0, 8),
		UDim2.new(0, Theme.space.md, 1, -18),
		Theme.color.good,
		{ segments = 10 }
	)
	assistBar.track.ZIndex = 2
	assistBar.fill.ZIndex = 3

	widgets = {
		panel = panel,
		assistCard = assistCard,
		assistText = assistText,
		assistHint = assistHint,
		assistBar = assistBar,
		track = track,
		marker = marker,
		goodZone = goodZone,
		perfectZone = perfectZone,
		title = title,
		pipRow = pipRow,
		pips = {},
		steps = steps,
		grade = grade,
		gradeScale = gradeScale,
		hint = hint,
	}

	UserInputService.InputBegan:Connect(function(input: InputObject, processed: boolean)
		if processed then
			return
		end
		local isAction = input.KeyCode == Enum.KeyCode.Space
			or input.UserInputType == Enum.UserInputType.MouseButton1
		if not isAction then
			return
		end
		if schedule ~= nil then
			-- Cerrahim: kesiyorum.
			press()
		elseif assistState.canAssist == true then
			-- Asistanim: Bosluk'a BASILI TUTUYORUM, kararlilik cubugu
			-- boyle yukarida kaliyor.
			assistHeld = true
			SurgeryPanel.pulseAssist()
		end
	end)

	UserInputService.InputEnded:Connect(function(input: InputObject)
		if input.KeyCode == Enum.KeyCode.Space or input.UserInputType == Enum.UserInputType.MouseButton1 then
			assistHeld = false
		end
	end)
end

--- Sunucu ameliyati baslatti.
function SurgeryPanel.begin(incoming: any)
	if widgets == nil then
		return
	end
	schedule = incoming
	table.clear(sentSteps)

	Theme.open(widgets.panel)
	widgets.grade.Text = ""

	-- Adim noktalari her ameliyatta yeniden kuruluyor: adim sayisi
	-- yukseltmelerle degisebiliyor.
	widgets.pipRow:ClearAllChildren()
	local pipLayout = Theme.list(widgets.pipRow, Theme.space.xs, Enum.FillDirection.Horizontal)
	pipLayout.HorizontalAlignment = Enum.HorizontalAlignment.Center
	widgets.pips = {}
	for index = 1, incoming.steps do
		table.insert(widgets.pips, Theme.frame({
			name = "Nokta" .. index,
			size = UDim2.fromOffset(10, 10),
			color = Theme.color.strokeSoft,
			transparency = 0.3,
			radius = 5,
			parent = widgets.pipRow,
			layoutOrder = index,
			zIndex = 3,
		}))
	end
	-- Baslik ve ipucu AMELIYAT TURUNDEN geliyor: dikis ile kirik
	-- hizalama ayni yazilari paylasmiyor.
	widgets.title.Text = Loc.get(incoming.nameKey or "surgery.title")
	widgets.hint.Text = Loc.get(incoming.hintKey or "surgery.hint")

	-- Bolgeler, isaretciyle AYNI fonksiyondan geciyor (placeOnTrack).
	-- Bolge duz, isaretci egri cizilseydi ekran yalan soylerdi.
	local motion = incoming.motion or "linear"
	local function zone(window: number): (number, number)
		local half = window / incoming.stepSeconds
		local left = placeOnTrack(motion, 0.5 - half)
		local right = placeOnTrack(motion, 0.5 + half)
		return left, right
	end

	local goodLeft, goodRight = zone(incoming.goodWindow)
	widgets.goodZone.Position = UDim2.new((goodLeft + goodRight) * 0.5, 0, 0, 0)
	widgets.goodZone.Size = UDim2.fromOffset(
		math.max(8, TRACK_WIDTH * (goodRight - goodLeft)),
		34
	)
	local perfectLeft, perfectRight = zone(incoming.perfectWindow)
	widgets.perfectZone.Position = UDim2.new((perfectLeft + perfectRight) * 0.5, 0, 0, 0)
	widgets.perfectZone.Size = UDim2.fromOffset(
		math.max(6, TRACK_WIDTH * (perfectRight - perfectLeft)),
		34
	)

	if connection ~= nil then
		connection:Disconnect()
	end
	connection = RunService.RenderStepped:Connect(function()
		if schedule == nil then
			return
		end
		local now = Workspace:GetServerTimeNow()
		local step = currentStep(now)
		local target = schedule.startTime + step * schedule.stepSeconds
		local ratio = math.clamp(0.5 + (now - target) / schedule.stepSeconds, 0, 1)

		widgets.marker.Position = UDim2.new(placeOnTrack(motion, ratio), 0, 0.5, 0)
		-- Isaretci mukemmel bolgedeyken beyazdan yesile donuyor:
		-- basma anini yalnizca konumdan degil, renkten de okuyabilmek
		-- gecikmeli oynayan oyuncuya yardim ediyor.
		local distance = math.abs(ratio - 0.5) * schedule.stepSeconds
		widgets.marker.BackgroundColor3 = if distance <= schedule.perfectWindow
			then Theme.color.good
			elseif distance <= schedule.goodWindow then Theme.color.accentGlow
			else Theme.color.text
		widgets.steps.Text = Loc.get("surgery.step", step, schedule.steps)
	end)
end

--- Asistan darbesi. Bosluk basili tutuldugu surece kendini tekrar
--- cagiriyor; sunucudaki kararlilik cubugu boyle yukarida kaliyor.
--- Hiz sabit (saniyede ~7): daha hizli basmak avantaj saglamasin.
function SurgeryPanel.pulseAssist()
	if not assistHeld or assistState.canAssist ~= true then
		return
	end
	Net.toServer("SurgeryAssist")
	task.delay(0.14, SurgeryPanel.pulseAssist)
end

--- Sunucudan gelen asistanlik durumu.
function SurgeryPanel.assist(payload: any)
	if widgets == nil or payload == nil then
		return
	end
	assistState = payload

	-- Cerrahin kendisi de kararliligi goruyor ama "asistanlik et"
	-- cagrisini gormuyor.
	local show = payload.active == true and (payload.canAssist == true or schedule ~= nil)
	if show ~= widgets.assistCard.Visible then
		if show then
			Theme.open(widgets.assistCard)
		else
			Theme.close(widgets.assistCard)
		end
	end
	if not show then
		return
	end

	if schedule ~= nil then
		widgets.assistText.Text = Loc.get("surgery.assist.stability")
		widgets.assistHint.Text = Loc.get("surgery.assist.surgeon")
	elseif payload.assisting == true then
		widgets.assistText.Text = Loc.get("surgery.assist.active", payload.surgeon or "?")
		widgets.assistHint.Text = Loc.get("surgery.assist.hint")
	else
		widgets.assistText.Text = Loc.get("surgery.assist.call", payload.surgeon or "?")
		widgets.assistHint.Text = Loc.get("surgery.assist.hint")
	end

	local stability = payload.stability or 0
	Theme.setBar(widgets.assistBar, stability)
	Theme.setBarColor(widgets.assistBar, Theme.statusColor(stability))
end

--- Sunucu bir tiklamayi notlandirdi.
function SurgeryPanel.grade(grade: string)
	if widgets == nil then
		return
	end
	local color = Theme.color.bad
	if grade == "perfect" then
		widgets.grade.Text = Loc.get("surgery.perfect")
		color = Theme.color.good
		Audio.play("surgeryPerfect")
	elseif grade == "good" then
		widgets.grade.Text = Loc.get("surgery.good")
		color = Theme.color.accentGlow
		Audio.play("surgeryGood")
	else
		widgets.grade.Text = Loc.get("surgery.miss")
		Audio.play("surgeryMiss")
	end
	widgets.grade.TextColor3 = color

	-- Not yazisi ZIPLAYARAK giriyor: ayni yazi ust uste iki kez
	-- cikarsa bile ikincisinin geldigi anlasiliyor.
	widgets.gradeScale.Scale = 1.35
	Theme.tween(widgets.gradeScale, { Scale = 1 }, Theme.motion.normal, Enum.EasingStyle.Back)

	local pip = widgets.pips[lastPressed]
	if pip ~= nil then
		pip.BackgroundColor3 = color
		Theme.tween(pip, { BackgroundTransparency = 0 }, Theme.motion.quick)
	end
end

function SurgeryPanel.finish()
	stop()
end

return SurgeryPanel
