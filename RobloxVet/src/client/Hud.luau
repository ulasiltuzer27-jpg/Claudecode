--!strict
-- Hud.luau
-- ========
-- Sol ustte para/seviye/itibar, sag ustte kuyruk ve kisayol ipuclari.

local ReplicatedStorage = game:GetService("ReplicatedStorage")
local Shared = ReplicatedStorage:WaitForChild("VetShared")
local Loc = require(Shared.Loc)

local Theme = require(script.Parent.Theme)

local Hud = {}

local widgets: any = nil

function Hud.create(parent: ScreenGui)
	local panel = Theme.frame({
		name = "Hud",
		size = UDim2.fromOffset(256, 132),
		position = UDim2.fromOffset(16, 16),
		parent = parent,
	})
	Theme.stroke(panel)

	local money = Theme.label({
		name = "Para",
		text = "",
		size = UDim2.new(1, -24, 0, 26),
		position = UDim2.fromOffset(12, 8),
		textSize = 22,
		bold = true,
		color = Theme.color.money,
		parent = panel,
	})

	local title = Theme.label({
		name = "Unvan",
		text = "",
		size = UDim2.new(1, -24, 0, 18),
		position = UDim2.fromOffset(12, 38),
		textSize = 13,
		color = Theme.color.textDim,
		parent = panel,
	})

	local xpBar = Theme.bar(
		panel,
		UDim2.new(1, -24, 0, 10),
		UDim2.fromOffset(12, 60),
		Theme.color.xp
	)
	local xpText = Theme.label({
		name = "Xp",
		text = "",
		size = UDim2.new(1, -24, 0, 14),
		position = UDim2.fromOffset(12, 72),
		textSize = 11,
		color = Theme.color.textDim,
		parent = panel,
	})

	local repBar = Theme.bar(
		panel,
		UDim2.new(1, -24, 0, 10),
		UDim2.fromOffset(12, 92),
		Theme.color.accent
	)
	local repText = Theme.label({
		name = "Itibar",
		text = "",
		size = UDim2.new(1, -24, 0, 14),
		position = UDim2.fromOffset(12, 104),
		textSize = 11,
		color = Theme.color.textDim,
		parent = panel,
	})

	-- Kalici kayit kapaliysa bunu SOYLE. Sessizce bellekte oynatmak,
	-- oyuncunun ilerlemesini kaybettiginde sebebini bilememesi demek.
	local warning = Theme.label({
		name = "Uyari",
		text = Loc.get("hud.transient"),
		size = UDim2.new(1, -24, 0, 16),
		position = UDim2.fromOffset(12, 122),
		textSize = 11,
		bold = true,
		color = Theme.color.warn,
		parent = panel,
	})
	warning.Visible = false

	local queue = Theme.frame({
		name = "Kuyruk",
		size = UDim2.fromOffset(210, 54),
		position = UDim2.new(1, -16, 0, 16),
		anchor = Vector2.new(1, 0),
		parent = parent,
	})
	Theme.stroke(queue)
	local queueText = Theme.label({
		name = "Sayac",
		text = "",
		size = UDim2.new(1, -24, 0, 22),
		position = UDim2.fromOffset(12, 8),
		textSize = 17,
		bold = true,
		parent = queue,
	})
	local hintText = Theme.label({
		name = "Ipucu",
		text = Loc.get("hud.hints"),
		size = UDim2.new(1, -24, 0, 16),
		position = UDim2.fromOffset(12, 32),
		textSize = 11,
		color = Theme.color.textDim,
		parent = queue,
	})

	widgets = {
		money = money,
		title = title,
		xpBar = xpBar,
		xpText = xpText,
		repBar = repBar,
		repText = repText,
		warning = warning,
		queueText = queueText,
		hintText = hintText,
	}
end

function Hud.update(state: any)
	if widgets == nil or state == nil or state.ready ~= true then
		return
	end

	widgets.money.Text = Loc.get("hud.money", state.money)
	widgets.title.Text = Loc.get("hud.level", state.level, Loc.get(state.titleKey))
	Theme.setBar(widgets.xpBar, state.progress)
	if state.level >= state.maxLevel then
		widgets.xpText.Text = Loc.get("hud.xpMax", state.xp)
	else
		widgets.xpText.Text = Loc.get("hud.xp", state.xp)
	end

	Theme.setBar(widgets.repBar, state.reputation / 100)
	widgets.repText.Text = Loc.get("hud.reputation", math.floor(state.reputation + 0.5))

	widgets.warning.Visible = state.transient == true
	widgets.queueText.Text = Loc.get("hud.queue", state.queueWaiting, state.queueSlots)
end

return Hud
