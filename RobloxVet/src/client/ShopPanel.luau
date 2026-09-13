--!strict
-- ShopPanel.luau
-- ==============
-- Magaza: aletler, klinik yukseltmeleri ve dekorasyon. B tusu ile aciliyor.
--
-- Fiyat/seviye denetimi burada YAPILMIYOR, yalnizca gosteriliyor.
-- Satin almanin gecerliligine sunucu karar veriyor (Upgrades.luau);
-- buradaki "kilitli" gorunum bir kolaylik, bir guvenlik onlemi degil.
--
-- Neden sekme?
-- ------------
-- Iki sutun yan yana dururken listeler kisaydi. Kategori sayisi ucе
-- ciktiginda yan yana sutun okunmaz hale geliyordu; sekme her kategoriye
-- panelin tamamini veriyor ve dorduncu kategori eklendiginde tasarim
-- degismek zorunda kalmiyor.

local ReplicatedStorage = game:GetService("ReplicatedStorage")
local Shared = ReplicatedStorage:WaitForChild("VetShared")
local Audio = require(Shared.Audio)
local Loc = require(Shared.Loc)
local Net = require(Shared.Net)

local Theme = require(script.Parent.Theme)

local ShopPanel = {}

local widgets: any = nil
local open = false
local lastSignature = ""
local activeTab = "tools"

type Entry = {
	id: string,
	nameKey: string,
	descKey: string?,
	price: number,
	owned: boolean,
	locked: boolean,
	unlockLevel: number,
	icon: string,
	onBuy: () -> (),
}

--- Tek bir magaza satiri: ikon, ad, aciklama, durum/fiyat.
local function entryCard(parent: Instance, order: number, entry: Entry)
	local dim = entry.locked or entry.owned
	local card = Theme.card({
		name = entry.id,
		size = UDim2.new(1, -Theme.space.sm, 0, if entry.descKey ~= nil then 68 else 52),
		color = Theme.color.panelLight,
		radius = Theme.radius.sm,
		layoutOrder = order,
		parent = parent,
		zIndex = 2,
	}, if entry.owned then Theme.color.good else Theme.color.strokeSoft)

	Theme.icon({
		name = entry.icon,
		size = 20,
		color = if dim then Theme.color.textFaint else Theme.color.accentGlow,
		position = UDim2.fromOffset(Theme.space.md, if entry.descKey ~= nil then 24 else 16),
		parent = card,
		zIndex = 3,
	})

	Theme.label({
		text = Loc.get(entry.nameKey),
		size = UDim2.new(1, -152, 0, 20),
		position = UDim2.fromOffset(44, Theme.space.sm),
		textSize = Theme.size.small,
		bold = true,
		color = if entry.locked then Theme.color.textDim else Theme.color.text,
		parent = card,
		zIndex = 3,
	})

	if entry.descKey ~= nil then
		Theme.label({
			text = Loc.get(entry.descKey),
			size = UDim2.new(1, -152, 0, 32),
			position = UDim2.fromOffset(44, 28),
			textSize = Theme.size.micro,
			color = Theme.color.textFaint,
			wrapped = true,
			parent = card,
			zIndex = 3,
		})
	end

	if entry.owned then
		local badge = Theme.badge(card, Loc.get("shop.owned"), Theme.color.good)
		badge.Position = UDim2.new(1, -Theme.space.md, 0.5, -10)
		badge.AnchorPoint = Vector2.new(1, 0)
		badge.ZIndex = 3
	elseif entry.locked then
		local badge = Theme.badge(card, Loc.get("shop.locked", entry.unlockLevel), Theme.color.textDim)
		badge.Position = UDim2.new(1, -Theme.space.md, 0.5, -10)
		badge.AnchorPoint = Vector2.new(1, 0)
		badge.ZIndex = 3
		Theme.icon({
			name = "lock",
			size = 12,
			color = Theme.color.textFaint,
			position = UDim2.new(1, -Theme.space.md - 96, 0.5, -6),
			parent = card,
			zIndex = 3,
		})
	else
		Theme.button({
			name = "SatinAl",
			text = Loc.get("shop.price", entry.price),
			size = UDim2.fromOffset(104, 30),
			position = UDim2.new(1, -Theme.space.md, 0.5, 0),
			anchor = Vector2.new(1, 0.5),
			textSize = Theme.size.small,
			color = Theme.color.accentDark,
			parent = card,
			zIndex = 3,
			onClick = entry.onBuy,
		})
	end
end

--- Sekme dugmesi. Secili sekme dolgulu, digerleri saydam.
local function tabButton(parent: Instance, id: string, labelKey: string, iconName: string, order: number): any
	local button = Theme.button({
		name = id,
		text = "",
		size = UDim2.fromOffset(168, 34),
		color = Theme.color.panelLight,
		layoutOrder = order,
		parent = parent,
		zIndex = 2,
		onClick = function()
			ShopPanel.setTab(id)
		end,
	})
	local icon = Theme.icon({
		name = iconName,
		size = 14,
		color = Theme.color.textDim,
		position = UDim2.fromOffset(Theme.space.md, 10),
		parent = button,
		zIndex = 3,
	})
	local label = Theme.label({
		text = Loc.get(labelKey),
		size = UDim2.new(1, -40, 1, 0),
		position = UDim2.fromOffset(36, 0),
		textSize = Theme.size.small,
		bold = true,
		color = Theme.color.textDim,
		parent = button,
		zIndex = 3,
	})
	return { button = button, icon = icon, label = label }
end

function ShopPanel.create(parent: ScreenGui)
	local panel = Theme.card({
		name = "Magaza",
		size = UDim2.fromOffset(600, 470),
		position = UDim2.new(0.5, 0, 0.5, 0),
		anchor = Vector2.new(0.5, 0.5),
		parent = parent,
		visible = false,
	}, Theme.color.accent)

	Theme.icon({
		name = "shop",
		size = 20,
		color = Theme.color.accentGlow,
		position = UDim2.fromOffset(Theme.space.lg, 14),
		parent = panel,
		zIndex = 2,
	})
	Theme.label({
		text = Loc.get("shop.title"),
		size = UDim2.new(1, -220, 0, 26),
		position = UDim2.fromOffset(Theme.space.lg + 28, 12),
		textSize = Theme.size.big,
		bold = true,
		parent = panel,
		zIndex = 2,
	})
	Theme.button({
		name = "Kapat",
		text = Loc.get("shop.close"),
		size = UDim2.fromOffset(96, 28),
		position = UDim2.new(1, -Theme.space.lg, 0, 12),
		anchor = Vector2.new(1, 0),
		color = Theme.color.panelLight,
		parent = panel,
		zIndex = 2,
		onClick = function()
			ShopPanel.setOpen(false)
		end,
	})

	local tabRow = Theme.frame({
		name = "Sekmeler",
		size = UDim2.new(1, -Theme.space.lg * 2, 0, 34),
		position = UDim2.fromOffset(Theme.space.lg, 50),
		transparency = 1,
		radius = 0,
		parent = panel,
		zIndex = 2,
	})
	local tabLayout = Theme.list(tabRow, Theme.space.sm, Enum.FillDirection.Horizontal)
	tabLayout.HorizontalAlignment = Enum.HorizontalAlignment.Left

	local tabs = {
		tools = tabButton(tabRow, "tools", "shop.tools", "tool", 1),
		upgrades = tabButton(tabRow, "upgrades", "shop.upgrades", "shop", 2),
		decor = tabButton(tabRow, "decor", "shop.decor", "decor", 3),
	}

	Theme.divider(panel, 0).Position = UDim2.new(0, Theme.space.lg, 0, 92)

	local list = Theme.scroller(
		panel,
		UDim2.new(1, -Theme.space.lg * 2, 1, -112),
		UDim2.fromOffset(Theme.space.lg, 100)
	)
	list.ZIndex = 2
	Theme.list(list, Theme.space.sm)

	local empty = Theme.label({
		name = "Bos",
		text = Loc.get("shop.emptyTab"),
		size = UDim2.new(1, -Theme.space.lg * 2, 0, 24),
		position = UDim2.fromOffset(Theme.space.lg, 130),
		textSize = Theme.size.small,
		align = Enum.TextXAlignment.Center,
		color = Theme.color.textFaint,
		parent = panel,
		visible = false,
		zIndex = 3,
	})

	widgets = { panel = panel, list = list, tabs = tabs, empty = empty }
	ShopPanel.setTab("tools")
end

--- Sekmeyi degistirir ve listeyi yeniden kurdurur.
function ShopPanel.setTab(id: string)
	if widgets == nil then
		return
	end
	activeTab = id
	for tabId, tab in widgets.tabs do
		local active = tabId == id
		Theme.tween(tab.button, {
			BackgroundColor3 = if active then Theme.color.accentDark else Theme.color.panelLight,
		}, Theme.motion.quick)
		tab.label.TextColor3 = if active then Theme.color.text else Theme.color.textDim
		tab.icon:Destroy()
	end
	-- Ikonun rengi Frame'lerin icinde; yeniden kurmak en ucuz yol.
	for tabId, tab in widgets.tabs do
		local active = tabId == id
		tab.icon = Theme.icon({
			name = if tabId == "tools" then "tool" elseif tabId == "upgrades" then "shop" else "decor",
			size = 14,
			color = if active then Theme.color.accentGlow else Theme.color.textFaint,
			position = UDim2.fromOffset(Theme.space.md, 10),
			parent = tab.button,
			zIndex = 3,
		})
	end
	lastSignature = ""
end

function ShopPanel.setOpen(value: boolean)
	if widgets == nil then
		return
	end
	open = value
	if value then
		Theme.open(widgets.panel)
	else
		Theme.close(widgets.panel)
	end
	Audio.play(if value then "uiOpen" else "uiClose")
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
	local signature = activeTab .. "|" .. tostring(state.level)
	for _, tool in state.shop.tools do
		signature ..= tool.id .. (if tool.owned then "1" else "0") .. (if tool.locked then "L" else "-")
	end
	for _, upgrade in state.shop.upgrades do
		signature ..= upgrade.id .. (if upgrade.owned then "1" else "0") .. (if upgrade.locked then "L" else "-")
	end
	for _, item in state.shop.decor or {} do
		signature ..= item.id .. (if item.owned then "1" else "0") .. (if item.locked then "L" else "-")
	end
	if signature == lastSignature then
		return
	end
	lastSignature = signature

	widgets.list:ClearAllChildren()
	Theme.list(widgets.list, Theme.space.sm)

	local count = 0
	if activeTab == "tools" then
		for index, tool in state.shop.tools do
			local id = tool.id
			count += 1
			entryCard(widgets.list, index, {
				id = id,
				nameKey = tool.nameKey,
				descKey = nil,
				price = tool.price,
				owned = tool.owned,
				locked = tool.locked,
				unlockLevel = tool.unlockLevel,
				icon = id,
				onBuy = function()
					Net.toServer("BuyTool", id)
				end,
			})
		end
	elseif activeTab == "upgrades" then
		for index, upgrade in state.shop.upgrades do
			local id = upgrade.id
			count += 1
			entryCard(widgets.list, index, {
				id = id,
				nameKey = upgrade.nameKey,
				descKey = upgrade.descKey,
				price = upgrade.price,
				owned = upgrade.owned,
				locked = upgrade.locked,
				unlockLevel = upgrade.unlockLevel,
				icon = "shop",
				onBuy = function()
					Net.toServer("BuyUpgrade", id)
				end,
			})
		end
	else
		for index, item in state.shop.decor or {} do
			local id = item.id
			count += 1
			entryCard(widgets.list, index, {
				id = id,
				nameKey = item.nameKey,
				descKey = item.descKey,
				price = item.price,
				owned = item.owned,
				locked = item.locked,
				unlockLevel = item.unlockLevel,
				icon = "decor",
				onBuy = function()
					Net.toServer("BuyDecor", id)
				end,
			})
		end
	end

	widgets.empty.Visible = count == 0
end

return ShopPanel
