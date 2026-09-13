--!strict
-- Tutorial.luau
-- =============
-- Ogreticinin gorunur tarafi: adim karti + vurgu halkasi.
--
-- Istemci hicbir sey KARAR VERMIYOR. Hangi adimda oldugumuzu sunucu
-- soyluyor (`state.tutorial`); burada yalnizca o adim ciziliyor ve
-- ilgili arayuz bolgesinin etrafina bir cerceve konuyor.
--
-- Neden "sonraki" dugmesi yok?
-- ----------------------------
-- Adim, isi YAPINCA kapaniyor. Bir "anladim" dugmesi koymak, hicbir
-- sey yapmadan ogreticiyi bitirmeyi mumkun kilardi ve ogretici de
-- hicbir sey ogretmezdi.

local ReplicatedStorage = game:GetService("ReplicatedStorage")
local Shared = ReplicatedStorage:WaitForChild("VetShared")
local Loc = require(Shared.Loc)

local Theme = require(script.Parent.Theme)

local Tutorial = {}

-- Vurgu bolgeleri: 1280x768 referans yerlesimindeki dikdortgenler.
-- `Theme.autoScale` butun arayuzu birlikte olcekledigi icin bu sayilar
-- her ekranda dogru kaliyor.
--
-- Tablodaki adlarin `src/server/game/Tutorial.luau` icindeki REGIONS
-- kumesiyle AYNI olmasi gerekiyor; verify_data ikisini karsilastiriyor.
local REGIONS: { [string]: { number } } = {
	-- alet cubugu (ToolBar): ekranin altinda, sag sutunun soluna kaydik
	toolbar = { 87, 668, 760, 84 },
	-- hasta karti (Chart)
	chart = { 934, 144, 330, 344 },
	-- teshis/tedavi paneli (ayni yuva)
	rightLower = { 934, 496, 330, 258 },
	-- sol ust: para, seviye, itibar, XP
	hud = { 16, 16, 306, 150 },
}

local card: Frame? = nil
local widgets: any = nil
local shownIndex = -1

function Tutorial.create(parent: ScreenGui)
	if card ~= nil then
		return
	end

	-- Vurgu cercevesi: saydam bir kare, yalnizca kenarligi gorunuyor.
	-- Koyu perde yerine cerceve secildi cunku perde, altindaki paneli
	-- okunmaz hale getiriyordu - oysa oyuncunun tam da onu okumasi
	-- gerekiyor.
	local ring = Theme.frame({
		name = "OgreticiVurgu",
		-- ui:katman — mevcut bir panelin ETRAFINA cizilen cerceve;
		-- kendi yuvasi yok, bu yuzden cakisma denetiminin disinda.
		size = UDim2.fromOffset(10, 10),
		position = UDim2.fromOffset(0, 0),
		transparency = 1,
		radius = Theme.radius.md,
		parent = parent,
	})
	Theme.stroke(ring, Theme.color.accentGlow, 2)
	ring.Visible = false
	ring.ZIndex = 20

	local panel = Theme.card({
		name = "Ogretici",
		-- Yukseklik 80'de kesiliyor: 286'dan itibaren ameliyat paneli
		-- basliyor ve tedavi adiminda ikisi ayni anda ekranda olabilir.
		size = UDim2.fromOffset(420, 80),
		-- Ekranin ust ortasinda, vardiya olayi seridinin altinda. Sol
		-- sutun (sayaclar, bildirimler, kogus, FPS) ve sag sutun (hasta
		-- karti, teshis/tedavi) dolu; olcunce kartin oraya SIGMADIGI
		-- cikti - `tools/verify_ui.py` iki cakismayi da yakaladi.
		position = UDim2.new(0.5, 0, 0, 204),
		anchor = Vector2.new(0.5, 0),
		color = Theme.color.panel,
		parent = parent,
	}, Theme.color.accentGlow)
	panel.ZIndex = 21
	panel.Visible = false

	-- Adim sayaci ile baslik AYNI satirda: kart 80 piksele sigmak
	-- zorunda ve metnin kendisi sayacin ustunde.
	local step = Theme.label({
		name = "Adim",
		text = "",
		size = UDim2.fromOffset(70, 18),
		position = UDim2.new(1, -Theme.space.md, 0, 8),
		anchor = Vector2.new(1, 0),
		textSize = Theme.size.micro,
		bold = true,
		align = Enum.TextXAlignment.Right,
		color = Theme.color.accentGlow,
		parent = panel,
		zIndex = 22,
	})
	local title = Theme.label({
		name = "Baslik",
		text = "",
		size = UDim2.new(1, -Theme.space.md * 2 - 74, 0, 18),
		position = UDim2.fromOffset(Theme.space.md, 8),
		textSize = Theme.size.small,
		bold = true,
		parent = panel,
		zIndex = 22,
	})
	local body = Theme.label({
		name = "Metin",
		text = "",
		size = UDim2.new(1, -Theme.space.md * 2, 0, 38),
		position = UDim2.fromOffset(Theme.space.md, 28),
		textSize = Theme.size.micro,
		color = Theme.color.textDim,
		wrapped = true,
		verticalAlign = Enum.TextYAlignment.Top,
		parent = panel,
		zIndex = 22,
	})

	local bar = Theme.bar(
		panel,
		UDim2.new(1, -Theme.space.md * 2, 0, 4),
		UDim2.new(0, Theme.space.md, 1, -10),
		Theme.color.accentGlow
	)
	bar.track.ZIndex = 22
	bar.fill.ZIndex = 23

	card = panel
	widgets = { panel = panel, ring = ring, step = step, title = title, body = body, bar = bar, pulse = nil }
end

--- Sunucudan gelen ogretici goruntusunu cizer. `nil` ise ogretici
--- bitmistir ve her sey gizleniyor.
function Tutorial.update(info: any)
	if widgets == nil then
		return
	end
	if info == nil then
		widgets.panel.Visible = false
		widgets.ring.Visible = false
		if widgets.pulse ~= nil then
			widgets.pulse.stop()
			widgets.pulse = nil
		end
		shownIndex = -1
		return
	end

	widgets.panel.Visible = true
	if shownIndex ~= info.index then
		shownIndex = info.index
		widgets.step.Text = Loc.get("tut.step", info.index, info.total)
		widgets.title.Text = Loc.get(info.titleKey)
		widgets.body.Text = Loc.get(info.bodyKey)
		Theme.setBar(widgets.bar, (info.index - 1) / math.max(1, info.total))
		Theme.flash(widgets.panel, Theme.color.accentGlow)

		local region = REGIONS[info.highlight]
		if region == nil then
			-- Bilinmeyen bolge SESSIZCE yutulmuyor: ekranda hicbir sey
			-- isaretlenmezse oyuncu nereye bakacagini bilemez.
			warn(("[Tutorial] bilinmeyen vurgu bolgesi: %s"):format(tostring(info.highlight)))
			widgets.ring.Visible = false
		else
			-- Cerceve panelin biraz DISINDAN geciyor ki paneli
			-- kapatmasin.
			local pad = 6
			widgets.ring.Position = UDim2.fromOffset(region[1] - pad, region[2] - pad)
			widgets.ring.Size = UDim2.fromOffset(region[3] + pad * 2, region[4] + pad * 2)
			widgets.ring.Visible = true
			if widgets.pulse ~= nil then
				widgets.pulse.stop()
			end
			local stroke = widgets.ring:FindFirstChildOfClass("UIStroke")
			if stroke ~= nil then
				widgets.pulse = Theme.pulse(stroke, "Transparency", 0.75, 0.9)
			end
		end
	end
end

--- Vurgu bolgesi adlari (verify_data sunucu tablosuyla karsilastiriyor).
function Tutorial.regions(): { string }
	local names = {}
	for name in REGIONS do
		table.insert(names, name)
	end
	table.sort(names)
	return names
end

return Tutorial
