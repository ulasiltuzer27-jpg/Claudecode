--!strict
-- SettingsPanel.luau
-- ==================
-- Ayarlar paneli (O tusu): kamera, sallanma, FOV, kare sayaci, kosma.
--
-- Neden kaydirici degil, "- deger +"?
-- -----------------------------------
-- Roblox'ta kaydirici hazir gelmiyor; elle yazilan bir kaydirici,
-- imlecin panelden cikmasi ve dokunmatik ekran gibi durumlarda kolayca
-- bozuluyor. Adimli dugmeler her girdi turunde ayni calisiyor ve
-- data/settings.json'daki `step` degerine dogrudan bagli.
--
-- Kaydetme gecikmeli
-- ------------------
-- Her dugme basisinda sunucuya yazmak DataStore kotasini bosa harcar.
-- Degisiklik once yerelde uygulaniyor (oyuncu aninda goruyor), sunucuya
-- yazma kisa bir sessizlikten sonra tek seferde gidiyor.

local ReplicatedStorage = game:GetService("ReplicatedStorage")
local Shared = ReplicatedStorage:WaitForChild("VetShared")
local Data = ReplicatedStorage:WaitForChild("VetData")

local Loc = require(Shared.Loc)
local Net = require(Shared.Net)

local Camera = require(script.Parent.Camera)
local Fps = require(script.Parent.Fps)
local Theme = require(script.Parent.Theme)

local settingsData = require(Data.settings)

local SettingsPanel = {}

local SAVE_DELAY = 0.75

local widgets: any = nil
local open = false
local values: any = nil
local saveToken = 0

local function scheduleSave()
	saveToken += 1
	local token = saveToken
	task.delay(SAVE_DELAY, function()
		if token ~= saveToken then
			return -- arkasindan baska bir degisiklik geldi
		end
		Net.toServer("SaveSettings", values)
	end)
end

local function applyLocally()
	Camera.apply(values)
	Fps.setVisible(values.showFps)
end

local function refresh()
	if widgets == nil or values == nil then
		return
	end
	widgets.rows.firstPerson.value.Text = Loc.get(if values.firstPerson then "set.on" else "set.off")
	widgets.rows.sprintEnabled.value.Text = Loc.get(if values.sprintEnabled then "set.on" else "set.off")
	widgets.rows.showFps.value.Text = Loc.get(if values.showFps then "set.on" else "set.off")
	widgets.rows.bobbing.value.Text = ("%.2f"):format(values.bobbing)
	widgets.rows.fieldOfView.value.Text = tostring(math.floor(values.fieldOfView + 0.5))
end

local function change(key: string, delta: number?)
	if values == nil then
		return
	end
	if delta == nil then
		values[key] = not values[key]
	else
		local limit = settingsData.limits[key]
		values[key] = math.clamp(values[key] + delta, limit.min, limit.max)
	end
	applyLocally()
	refresh()
	scheduleSave()
end

--- Tek ayar satiri. `limitKey` verilirse adimli sayi, verilmezse anahtar.
local function row(parent: Instance, order: number, key: string, labelKey: string, descKey: string, limitKey: string?)
	local card = Theme.frame({
		name = key,
		size = UDim2.new(1, -8, 0, 54),
		color = Theme.color.panelLight,
		transparency = 0.15,
		radius = 8,
		layoutOrder = order,
		parent = parent,
	})

	Theme.label({
		text = Loc.get(labelKey),
		size = UDim2.new(1, -150, 0, 20),
		position = UDim2.fromOffset(12, 6),
		textSize = 14,
		bold = true,
		parent = card,
	})
	Theme.label({
		text = Loc.get(descKey),
		size = UDim2.new(1, -150, 0, 24),
		position = UDim2.fromOffset(12, 26),
		textSize = 11,
		color = Theme.color.textDim,
		wrapped = true,
		parent = card,
	})

	local value = Theme.label({
		name = "Deger",
		text = "",
		size = UDim2.fromOffset(64, 26),
		position = UDim2.new(1, -86, 0.5, 0),
		anchor = Vector2.new(0, 0.5),
		textSize = 15,
		bold = true,
		align = Enum.TextXAlignment.Center,
		color = Theme.color.accent,
		parent = card,
	})

	if limitKey == nil then
		Theme.button({
			text = "⇄",
			size = UDim2.fromOffset(30, 26),
			position = UDim2.new(1, -42, 0.5, 0),
			anchor = Vector2.new(0, 0.5),
			parent = card,
			onClick = function()
				change(key, nil)
			end,
		})
	else
		local step = settingsData.limits[limitKey].step
		Theme.button({
			text = "−",
			size = UDim2.fromOffset(28, 26),
			position = UDim2.new(1, -128, 0.5, 0),
			anchor = Vector2.new(0, 0.5),
			parent = card,
			onClick = function()
				change(key, -step)
			end,
		})
		Theme.button({
			text = "+",
			size = UDim2.fromOffset(28, 26),
			position = UDim2.new(1, -40, 0.5, 0),
			anchor = Vector2.new(0, 0.5),
			parent = card,
			onClick = function()
				change(key, step)
			end,
		})
	end

	return { card = card, value = value }
end

function SettingsPanel.create(parent: ScreenGui)
	values = {
		firstPerson = settingsData.defaults.firstPerson,
		bobbing = settingsData.defaults.bobbing,
		fieldOfView = settingsData.defaults.fieldOfView,
		showFps = settingsData.defaults.showFps,
		sprintEnabled = settingsData.defaults.sprintEnabled,
	}

	local panel = Theme.frame({
		name = "Ayarlar",
		size = UDim2.fromOffset(460, 404),
		position = UDim2.new(0.5, 0, 0.5, 0),
		anchor = Vector2.new(0.5, 0.5),
		transparency = 0.03,
		parent = parent,
		visible = false,
	})
	Theme.stroke(panel, Theme.color.accent, 2)

	Theme.label({
		text = Loc.get("set.title"),
		size = UDim2.new(1, -24, 0, 26),
		position = UDim2.fromOffset(16, 10),
		textSize = 20,
		bold = true,
		parent = panel,
	})
	Theme.button({
		text = Loc.get("set.close"),
		size = UDim2.fromOffset(86, 26),
		position = UDim2.new(1, -102, 0, 10),
		color = Theme.color.panelLight,
		parent = panel,
		onClick = function()
			SettingsPanel.setOpen(false)
		end,
	})

	local list = Theme.scroller(panel, UDim2.new(1, -32, 1, -58), UDim2.fromOffset(16, 46))
	Theme.list(list, 6)

	local rows = {
		firstPerson = row(list, 1, "firstPerson", "set.firstPerson", "set.firstPerson.desc"),
		bobbing = row(list, 2, "bobbing", "set.bobbing", "set.bobbing.desc", "bobbing"),
		fieldOfView = row(list, 3, "fieldOfView", "set.fov", "set.fov.desc", "fieldOfView"),
		sprintEnabled = row(list, 4, "sprintEnabled", "set.sprint", "set.sprint.desc"),
		showFps = row(list, 5, "showFps", "set.fps", "set.fps.desc"),
	}

	widgets = { panel = panel, rows = rows }
	refresh()
end

--- Sunucudan gelen kayitli ayarlari yukler. Oyuncunun o an
--- degistirdiginin uzerine yazmamasi icin bekleyen bir kayit varsa
--- yok sayiliyor.
function SettingsPanel.load(settings: any)
	if settings == nil or values == nil then
		return
	end
	if saveToken > 0 then
		return
	end
	for key, value in settings do
		if values[key] ~= nil and type(value) == type(values[key]) then
			values[key] = value
		end
	end
	applyLocally()
	refresh()
end

function SettingsPanel.setOpen(value: boolean)
	if widgets == nil then
		return
	end
	open = value
	widgets.panel.Visible = value
end

function SettingsPanel.toggle()
	SettingsPanel.setOpen(not open)
end

--- V tusu: paneli acmadan birinci/ucuncu sahis arasinda gecis.
function SettingsPanel.quickTogglePerspective()
	if values == nil then
		return
	end
	values.firstPerson = Camera.toggleFirstPerson()
	refresh()
	scheduleSave()
end

return SettingsPanel
