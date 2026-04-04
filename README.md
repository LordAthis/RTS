Reparing's - Tuning's - Setting's
---------------------------------
# Javítások, Tuningok, BugFix-ek, Finomhangolások és Beállítók Windows-ra

Amolyan Szerszámos-Láda, amiben az összes témába vágó repó egységes keretrendszeren keresztül működtethető!
---------------------------------


Tervek:
Ebben a repozitoriban fogom összegyűjteni az elmúlt 25 év folyamán (2000 óta) összegyűjtött és használt rendszert optimalizáló, javító, hangoló, beállító kódokat, BugFixeket, Frissítéseket és programokat!
Igyekszem majd ezt minden jelenleg is használatban lévő rendszerre (és természetesen a RETRO jegyében a régebbiekre is!) alkalmazható formában közzétenni.

A használhatóság érdekében éppen ezért szét lesz szedve a többféle rendszer több különálló könyvtárra, ezért ismétlődések várhatóak ezekben a kódokban,
azonban könyebben használható, átlátható lesz, ezáltal könnyebben modosítható is!
RTS/
├── bootstrap.ps1          ← Ez fut le először klónozás után
├── update_check.ps1       ← Frissítésfigyelő (kézi + automatikus)
├── updates.json           ← Eltárolja az utolsó lekérdezés eredményét
├── modules.json           ← A modulok listája és GitHub URL-je
├── src/                   ← RTS saját kódja (GUI, launcher)
├── Apps/                  ← Letöltött modulok ide kerülnek
│   ├── IWS/
│   ├── RescueData/
│   ├── Network-Tools/
│   └── ...
└── _archive/






# A Frissítések letöltésének, és a fájlok elrendezésének vázlata
---------------------------------


