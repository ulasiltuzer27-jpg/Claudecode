--!strict
-- StaffFactory.luau
-- =================
-- Ise alinan personelin klinikteki NPC'si.
--
-- Neden yeni bir rig yazilmadi?
-- -----------------------------
-- `OwnerFactory` zaten bir insan rigi kuruyor: govde, kafa, iki kol,
-- iki bacak, eklemler, yuruyus ve bakis. Ikinci bir insan rigi yazmak
-- ayni 200 satiri iki yerde tutmak olurdu; hata da iki yerde
-- duzeltilmek zorunda kalirdi. Personel ayni rigi kullaniyor, tek farki
-- ONLUGUNUN RENGI (data/staff.json -> coat) ve basindaki yaziyla
-- gorevi.
--
-- Ne yapiyorlar?
-- --------------
-- Klinikte devriye geziyorlar. Etkileri Staff.luau'da ve NPC'den
-- BAGIMSIZ: NPC yok edilse bile etki devam eder. NPC gorsel bir
-- karsilik - oyuncu parasinin nereye gittigini gorsun diye var.

local ReplicatedStorage = game:GetService("ReplicatedStorage")
local Shared = ReplicatedStorage:WaitForChild("VetShared")
local Data = ReplicatedStorage:WaitForChild("VetData")

local Build = require(Shared.Build)

local OwnerFactory = require(script.Parent.OwnerFactory)

local staffData = require(Data.staff)
local clinicPlan = require(Data.clinic)

local StaffFactory = {}

type Member = {
	id: string,
	owner: OwnerFactory.Owner,
	waypoint: number,
}

local members: { [string]: Member } = {}
local folder: Folder? = nil
local running = false

local byId: { [string]: any } = {}
for _, entry in staffData.staff do
	byId[entry.id] = entry
end

--- Devriye noktalarini dunya koordinatina cevirir.
local function patrolPoints(): { Vector3 }
	local points: { Vector3 } = {}
	for _, point in staffData.patrol do
		table.insert(points, Vector3.new(point[1], clinicPlan.floorY, point[2]))
	end
	return points
end

local waypoints: { Vector3 } = {}

--- Bir sonraki noktaya yurut. Varinca kendini tekrar cagiriyor, yani
--- devriye kesintisiz donuyor.
local function sendToNext(member: Member)
	if #waypoints == 0 then
		return
	end
	member.waypoint = member.waypoint % #waypoints + 1
	local target = waypoints[member.waypoint]
	OwnerFactory.walkPath(member.owner, { target }, function()
		if members[member.id] ~= member then
			return -- bu arada isten ayrildi
		end
		-- Noktada kisa bir mola: durmadan yuruyen bir NPC huzursuz
		-- gorunuyor.
		task.delay(1.5 + math.random() * 2.5, function()
			if members[member.id] == member then
				sendToNext(member)
			end
		end)
	end)
end

--- Klinik kurulunca cagriliyor: devriye rotasini ve klasoru hazirlar.
function StaffFactory.start(parent: Instance)
	waypoints = patrolPoints()
	if folder == nil then
		local created = Instance.new("Folder")
		created.Name = "Personel"
		created.Parent = parent
		folder = created
	end
	if running then
		return
	end
	-- Personel de sahipler gibi OwnerFactory'nin kendi dongusunde
	-- animasyon aliyor; burada ayri bir Heartbeat'e gerek yok.
	running = true
end

--- Personeli klinige ekler. Zaten varsa hicbir sey yapmiyor.
function StaffFactory.hire(id: string): boolean
	if members[id] ~= nil or folder == nil then
		return false
	end
	local entry = byId[id]
	if entry == nil then
		return false
	end

	local spawnAt = if #waypoints > 0 then waypoints[1] else Vector3.new(0, clinicPlan.floorY, 0)
	local owner = OwnerFactory.create(
		CFrame.new(spawnAt),
		entry.nameKey,
		folder,
		Build.color(entry.coat)
	)
	-- Personelin memnuniyet olcusu yok: o cubuk sahibin sabri icin.
	owner.meter.Visible = false
	OwnerFactory.register(owner)

	local member: Member = { id = id, owner = owner, waypoint = 1 }
	members[id] = member
	sendToNext(member)
	return true
end

--- Personeli klinikten cikarir (isten ayrilma ya da oyuncu ayrilmasi).
function StaffFactory.dismiss(id: string): boolean
	local member = members[id]
	if member == nil then
		return false
	end
	members[id] = nil
	OwnerFactory.unregister(member.owner)
	member.owner.model:Destroy()
	return true
end

--- Klinikte olmasi gereken personeli getirir, gerekmeyeni gonderir.
--- Cok oyunculuda kimin ise aldigi onemli degil: klinikte bir
--- resepsiyonist ya vardir ya yoktur.
function StaffFactory.sync(wanted: { [string]: boolean })
	for id in byId do
		local shouldExist = wanted[id] == true
		local exists = members[id] ~= nil
		if shouldExist and not exists then
			StaffFactory.hire(id)
		elseif not shouldExist and exists then
			StaffFactory.dismiss(id)
		end
	end
end

--- Klinikteki personel sayisi (SelfTest olcuyor).
function StaffFactory.count(): number
	local total = 0
	for _ in members do
		total += 1
	end
	return total
end

return StaffFactory
