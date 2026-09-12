--!strict
-- ShopPanel.luau
-- ==============
-- Magaza: aletler ve klinik yukseltmeleri. B tusu ile aciliyor.
--
-- Fiyat/seviye denetimi burada YAPILMIYOR, yalnizca gosteriliyor.
-- Satin almanin gecerliligine sunucu karar veriyor (Upgrades.luau);
-- buradaki "kilitli" gorunum bir kolaylik, bir guvenlik onlemi degil.

local ReplicatedStorage = game:GetService("ReplicatedStorage")
local Shared = ReplicatedStorage:WaitForChild("VetShared")
local Loc = require(Shared.Loc)
local Net = require(Shared.Net)

local Theme = require(script.Parent.Theme)

local ShopPanel = {}

local widgets: any = nil
local open = false
local lastSignature = ""

local function entryCard(
	parent: Instance,
	order: number,
	nameKey: string,
	descKey: string?,
	price: number,
	owned: boolean,
	locked: boolean,
	unlockLevel: number,
	onBuy: () -> ()
)
	local card = Theme.frame({
		name = nameKey,
		size = UDim2.new(1, -8, 0, if descKey ~= nil then 62 else 46),
		color = Theme.color.panelLight,
		transparency = 0.15,
		radius = 8,
		layoutOrder = order,
		parent = parent,
	})

	Theme.label({
		text = Loc.get(nameKey),
		size = UDim2.new(1, -110, 0, 20),
		position = UDim2.fromOffset(10, 6),
		textSize = 14,
		bold = true,
		color = if locked then Theme.color.textDim else Theme.color.text,
		parent = card,
	})

	if descKey ~= nil then
		Theme.label({
			text = Loc.get(descKey),
			size = UDim2.new(1, -110, 0, 30),
			position = UDim2.fromOffset(10, 26),
			textSize = 11,
			color = Theme.color.textDim,
			wrapped = true,
			parent = card,
		})
	end

	if owned then
		Theme.label({
			text = Loc.get("shop.owned"),
			size = UDim2.fromOffset(92, 26),
			position = UDim2.new(1, -100, 0.5, 0),
			anchor = Vector2.new(0, 0.5),
			textSize = 13,
			bold = true,
			align = Enum.TextXAlignment.Center,
			color = Theme.color.good,
			parent = card,
		})
	elseif locked then
		Theme.label({
			text = Loc.get("shop.locked", unlockLevel),
			size = UDim2.fromOffset(92, 26),
			position = UDim2.new(1, -100, 0.5, 0),
			anchor = Vector2.new(0, 0.5),
			textSize = 12,
			bold = true,
			align = Enum.TextXAlignment.Center,
			color = Theme.color.textDim,
			parent = card,
		})
	else
		Theme.button({
			text = ("%d TL"):format(price),
			size = UDim2.fromOffset(92, 26),
			position = UDim2.new(1, -100, 0.5, 0),
			anchor = Vector2.new(0, 0.5),
			textSize = 13,
			color = Theme.color.accentDark,
			parent = card,
			onClick = onBuy,
		})
	end
end

function ShopPanel.create(parent: ScreenGui)
	local panel = Theme.frame({
		name = "Magaza",
		size = UDim2.fromOffset(720, 440),
		position = UDim2.new(0.5, 0, 0.5, 0),
		anchor = Vector2.new(0.5, 0.5),
		transparency = 0.03,
		parent = parent,
		visible = false,
	})
	Theme.stroke(panel, Theme.color.accent, 2)

	Theme.label({
		text = Loc.get("shop.title"),
		size = UDim2.new(1, -24, 0, 26),
		position = UDim2.fromOffset(16, 10),
		textSize = 20,
		bold = true,
		parent = panel,
	})
	Theme.button({
		name = "Kapat",
		text = Loc.get("shop.close"),
		size = UDim2.fromOffset(86, 26),
		position = UDim2.new(1, -102, 0, 10),
		color = Theme.color.panelLight,
		parent = panel,
		onClick = function()
			ShopPanel.setOpen(false)
		end,
	})

	Theme.label({
		text = Loc.get("shop.tools"),
		size = UDim2.fromOffset(320, 20),
		position = UDim2.fromOffset(16, 44),
		textSize = 14,
		bold = true,
		color = Theme.color.accent,
		parent = panel,
	})
	Theme.label({
		text = Loc.get("shop.upgrades"),
		size = UDim2.fromOffset(320, 20),
		position = UDim2.fromOffset(376, 44),
		textSize = 14,
		bold = true,
		color = Theme.color.accent,
		parent = panel,
	})

	local tools = Theme.scroller(panel, UDim2.fromOffset(340, 366), UDim2.fromOffset(16, 66))
	Theme.list(tools, 6)
	local upgrades = Theme.scroller(panel, UDim2.fromOffset(340, 366), UDim2.fromOffset(368, 66))
	Theme.list(upgrades, 6)

	widgets = { panel = panel, tools = tools, upgrades = upgrades }
end

function ShopPanel.setOpen(value: boolean)
	if widgets == nil then
		return
	end
	open = value
	widgets.panel.Visible = value
end

function ShopPanel.toggle()
	ShopPanel.setOpen(not open)
end

function ShopPanel.update(state: any)
	if widgets == nil or state == nil or state.ready ~= true or state.shop == nil then
		return
	end

	-- Liste yalnizca degistiyse yeniden kuruluyor: para her taburcuda
	-- degisiyor ve her seferinde dugmeleri yeniden yaratmak, oyuncunun
	-- tiklamaya calistigi dugmeyi altindan cekerdi.
	local signature = tostring(state.level) .. "|" .. tostring(state.money >= 0)
	for _, tool in state.shop.tools do
		signature ..= tool.id .. (if tool.owned then "1" else "0") .. (if tool.locked then "L" else "-")
	end
	for _, upgrade in state.shop.upgrades do
		signature ..= upgrade.id .. (if upgrade.owned then "1" else "0") .. (if upgrade.locked then "L" else "-")
	end
	if signature == lastSignature then
		return
	end
	lastSignature = signature

	widgets.tools:ClearAllChildren()
	Theme.list(widgets.tools, 6)
	for index, tool in state.shop.tools do
		local id = tool.id
		entryCard(widgets.tools, index, tool.nameKey, nil, tool.price, tool.owned, tool.locked, tool.unlockLevel, function()
			Net.toServer("BuyTool", id)
		end)
	end

	widgets.upgrades:ClearAllChildren()
	Theme.list(widgets.upgrades, 6)
	for index, upgrade in state.shop.upgrades do
		local id = upgrade.id
		entryCard(
			widgets.upgrades,
			index,
			upgrade.nameKey,
			upgrade.descKey,
			upgrade.price,
			upgrade.owned,
			upgrade.locked,
			upgrade.unlockLevel,
			function()
				Net.toServer("BuyUpgrade", id)
			end
		)
	end
end

return ShopPanel
