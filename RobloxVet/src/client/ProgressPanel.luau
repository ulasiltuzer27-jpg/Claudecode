--!strict
-- ProgressPanel.luau
-- ==================
-- Gunluk gorevler · Basarimlar · Istatistikler (G tusu).
--
-- Uc sekme tek panelde: hepsi "ne kadar ilerledim" sorusunun cevabi ve
-- ayri ayri tuslara dagitmak oyuncuyu ezberlemeye zorlardi.
--
-- Icerik SUNUCUDAN geliyor (ProgressUpdate). Istemci hicbir ilerleme
-- hesaplamiyor; yalnizca ciziyor.

local ReplicatedStorage = game:GetService("ReplicatedStorage")
local Shared = ReplicatedStorage:WaitForChild("VetShared")

local Loc = require(Shared.Loc)
local Net = require(Shared.Net)

local Theme = require(script.Parent.Theme)

local ProgressPanel = {}

local widgets: any = nil
local open = false
local tab = "tasks"
local latest: any = nil

local function formatDuration(seconds: number): string
	local hours = math.floor(seconds / 3600)
	local minutes = math.floor((seconds % 3600) / 60)
	return ("%02d:%02d"):format(hours, minutes)
end

--- Ilerleme cubuklu tek satir. Gorev ve basarim ayni gorunumu
--- paylasiyor: ikisi de "hedefe ne kadar kaldi" anlatiyor.
local function progressCard(
	parent: Instance,
	order: number,
	titleKey: string,
	descKey: string?,
	value: number,
	target: number,
	done: boolean,
	money: number,
	xp: number
)
	local card = Theme.frame({
		size = UDim2.new(1, -8, 0, if descKey ~= nil then 74 else 58),
		color = Theme.color.panelLight,
		transparency = 0.15,
		radius = 8,
		layoutOrder = order,
		parent = parent,
	})

	Theme.label({
		text = (if done then "✓ " else "") .. Loc.get(titleKey),
		size = UDim2.new(1, -24, 0, 20),
		position = UDim2.fromOffset(12, 6),
		textSize = 14,
		bold = true,
		color = if done then Theme.color.good else Theme.color.text,
		parent = card,
	})

	local top = 26
	if descKey ~= nil then
		Theme.label({
			text = Loc.get(descKey),
			size = UDim2.new(1, -24, 0, 16),
			position = UDim2.fromOffset(12, top),
			textSize = 11,
			color = Theme.color.textDim,
			parent = card,
		})
		top += 18
	end

	local bar = Theme.bar(card, UDim2.new(1, -24, 0, 8), UDim2.fromOffset(12, top + 2), if done then Theme.color.good else Theme.color.accent)
	Theme.setBar(bar, if target > 0 then value / target else 0)

	Theme.label({
		text = ("%d / %d"):format(value, target),
		size = UDim2.new(0.5, -12, 0, 16),
		position = UDim2.fromOffset(12, top + 12),
		textSize = 11,
		color = Theme.color.textDim,
		parent = card,
	})
	Theme.label({
		text = Loc.get("progress.reward", money, xp),
		size = UDim2.new(0.5, -12, 0, 16),
		position = UDim2.new(0.5, 0, 0, top + 12),
		textSize = 11,
		align = Enum.TextXAlignment.Right,
		color = Theme.color.money,
		parent = card,
	})
end

local function statRow(parent: Instance, order: number, labelKey: string, value: any)
	local card = Theme.frame({
		size = UDim2.new(1, -8, 0, 28),
		color = Theme.color.panelLight,
		transparency = 0.3,
		radius = 6,
		layoutOrder = order,
		parent = parent,
	})
	Theme.label({
		text = Loc.get(labelKey),
		size = UDim2.new(1, -120, 1, 0),
		position = UDim2.fromOffset(12, 0),
		textSize = 13,
		parent = card,
	})
	Theme.label({
		text = tostring(value),
		size = UDim2.fromOffset(100, 28),
		position = UDim2.new(1, -112, 0, 0),
		textSize = 13,
		bold = true,
		align = Enum.TextXAlignment.Right,
		color = Theme.color.accent,
		parent = card,
	})
end

local function redraw()
	if widgets == nil or latest == nil then
		return
	end

	widgets.list:ClearAllChildren()
	Theme.list(widgets.list, 6)

	for name, button in widgets.tabs do
		button.BackgroundColor3 = if name == tab then Theme.color.accentDark else Theme.color.panelLight
	end

	if tab == "tasks" then
		local tasks = latest.tasks
		widgets.subtitle.Text = Loc.get("progress.tasksSub", tasks.done, tasks.total, formatDuration(tasks.resetsIn))
		for index, task in tasks.list do
			progressCard(widgets.list, index, task.nameKey, nil, task.value, task.target, task.done, task.money, task.xp)
		end
	elseif tab == "achievements" then
		local achievements = latest.achievements
		widgets.subtitle.Text = Loc.get("progress.achSub", achievements.unlocked, achievements.total)
		for index, achievement in achievements.list do
			progressCard(
				widgets.list,
				index,
				achievement.nameKey,
				achievement.descKey,
				achievement.value,
				achievement.target,
				achievement.unlocked,
				achievement.money,
				achievement.xp
			)
		end
	else
		local stats = latest.stats
		local total = stats.correct + stats.wrong
		local accuracy = if total > 0 then math.floor(stats.correct / total * 100 + 0.5) else 0
		widgets.subtitle.Text = Loc.get("progress.statsSub", accuracy)

		statRow(widgets.list, 1, "stat.patients", stats.patients)
		statRow(widgets.list, 2, "stat.correct", stats.correct)
		statRow(widgets.list, 3, "stat.wrong", stats.wrong)
		statRow(widgets.list, 4, "stat.accuracy", accuracy .. "%")
		statRow(widgets.list, 5, "stat.streak", stats.streak)
		statRow(widgets.list, 6, "stat.bestStreak", stats.bestStreak)
		statRow(widgets.list, 7, "stat.earned", stats.earned)
		statRow(widgets.list, 8, "stat.tips", stats.tips)
		statRow(widgets.list, 9, "stat.surgeries", stats.surgeries)
		statRow(widgets.list, 10, "stat.perfectSurgeries", stats.perfectSurgeries)
		statRow(widgets.list, 11, "stat.emergencies", stats.emergencies)
		statRow(widgets.list, 12, "stat.lost", stats.lost)
		statRow(widgets.list, 13, "stat.tasks", stats.tasks)
	end
end

function ProgressPanel.create(parent: ScreenGui)
	local panel = Theme.frame({
		name = "Ilerleme",
		size = UDim2.fromOffset(520, 452),
		position = UDim2.new(0.5, 0, 0.5, 0),
		anchor = Vector2.new(0.5, 0.5),
		transparency = 0.03,
		parent = parent,
		visible = false,
	})
	Theme.stroke(panel, Theme.color.accent, 2)

	Theme.label({
		text = Loc.get("progress.title"),
		size = UDim2.new(1, -24, 0, 26),
		position = UDim2.fromOffset(16, 10),
		textSize = 20,
		bold = true,
		parent = panel,
	})
	Theme.button({
		text = Loc.get("progress.close"),
		size = UDim2.fromOffset(86, 26),
		position = UDim2.new(1, -102, 0, 10),
		color = Theme.color.panelLight,
		parent = panel,
		onClick = function()
			ProgressPanel.setOpen(false)
		end,
	})

	local tabs: { [string]: TextButton } = {}
	local names = { { "tasks", "progress.tabTasks" }, { "achievements", "progress.tabAch" }, { "stats", "progress.tabStats" } }
	for index, entry in names do
		local name = entry[1]
		tabs[name] = Theme.button({
			name = name,
			text = Loc.get(entry[2]),
			size = UDim2.fromOffset(154, 28),
			position = UDim2.fromOffset(16 + (index - 1) * 162, 44),
			parent = panel,
			onClick = function()
				tab = name
				redraw()
			end,
		})
	end

	local subtitle = Theme.label({
		name = "AltBaslik",
		text = "",
		size = UDim2.new(1, -32, 0, 18),
		position = UDim2.fromOffset(16, 78),
		textSize = 12,
		color = Theme.color.textDim,
		parent = panel,
	})

	local list = Theme.scroller(panel, UDim2.new(1, -32, 1, -112), UDim2.fromOffset(16, 100))
	Theme.list(list, 6)

	widgets = { panel = panel, tabs = tabs, subtitle = subtitle, list = list }
end

function ProgressPanel.update(progress: any)
	latest = progress
	if open then
		redraw()
	end
end

function ProgressPanel.setOpen(value: boolean)
	if widgets == nil then
		return
	end
	open = value
	widgets.panel.Visible = value
	if value then
		-- Panel acilirken taze veri iste: kapaliyken gelen guncellemeler
		-- cizilmiyor, o yuzden acilista bir kez sormak gerekiyor.
		Net.toServer("RequestProgress")
		redraw()
	end
end

function ProgressPanel.toggle()
	ProgressPanel.setOpen(not open)
end

return ProgressPanel
