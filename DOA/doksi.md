\## THTML Templating system



Ez a dokumentum részletesen bemutatja, hogyan lehet riportokat készíteni a `.thtml` sablonnyelv segítségével. A sablonok egyszerű szövegfájlok, amelyek HTML kódot és speciális, `{{...}}` jelek közé zárt riport-komponenseket tartalmazhatnak.



---



\### Alapvető Szintaxis



Minden riport-komponens egy `{{ }}` blokkban helyezkedik el. A blokkon belül kulcs-érték párokkal, pontosvesszővel (`;`) elválasztva adhatjuk meg a komponens tulajdonságait.



\*\*Alapvető szerkezet:\*\*

```html

{{

&nbsp; query="SELECT ... FROM ...";

&nbsp; representation="típus";

&nbsp; parameter1="érték1";

&nbsp; parameter2="érték2";

}}

```

A string értékeket idézőjelek (`"`) közé kell tenni. A numerikus és logikai értékek (pl. `true`/`false`) írhatók idézőjelek nélkül is.



---



\### 1. Adat-komponensek



Ezek a komponensek egy SQL lekérdezés eredményét jelenítik meg valamilyen vizuális formában.



\#### \*\*Általános paraméterek (minden adat-komponensre érvényesek)\*\*



| Paraméter | Típus | Kötelező? | Leírás | Példa |

| :--- | :--- | :--- | :--- | :--- |

| `query` | string | \*\*Igen\*\* | A futtatandó SQL lekérdezés. Használhat `@parameter` jelölőket a dinamikus szűréshez. | `query="SELECT Ország, Bevétel FROM eladások WHERE Év = @EvParam"` |

| `representation` | string | \*\*Igen\*\* | A megjelenítés típusa. Lehet: `table`, `barchart`, `linechart`, `piechart`. | `representation="barchart"` |

| `series` | string / array | \*\*Igen\*\* | Az(ok) az oszlop(ok), amelyek a numerikus adatokat tartalmazzák. | `series="Bevétel"` vagy `series=\["Bevétel", "Kiadás"]` |

| `legends` | string | Nem | Az az oszlop, amely a kategóriákat, címkéket tartalmazza (pl. a diagram X-tengelye). Alapértelmezetten az első oszlop. | `legends="Ország"` |

| `groupBy` | string | Nem | Az az oszlop, amely alapján a `series` adatsorokat csoportosítani kell. Minden egyedi érték új adatsort (pl. új vonalat vagy oszlopcsoportot) hoz létre. | `groupBy="Termékkategória"` |

| `sortBy` | string | Nem | Az az oszlop, amely alapján az adatokat a megjelenítés előtt rendezni kell. | `sortBy="Bevétel"` |

| `sortDirection` | string | Nem | A rendezés iránya. Lehet `asc` (növekvő) vagy `desc` (csökkenő). Alapértelmezett: `asc`. | `sortDirection="desc"` |

| `formatting` | object | Nem | Egy speciális objektum a komponens részletes formázásához. Lásd a "Formázás" fejezetet. | `formatting={title:"Eladások"}` |



---



\#### \*\*Táblázat (`table`)\*\*

Egy interaktív, kereshető és rendezhető táblázatot jelenít meg a \[DataTables.js](https://datatables.net/) segítségével.



\*\*Példa:\*\*

```html

{{

&nbsp; query="SELECT CountryRegionCode, SUM(LineTotal) as TotalRevenue FROM Sales.vSalesPerson GROUP BY CountryRegionCode";

&nbsp; representation="table";

&nbsp; series="TotalRevenue";

&nbsp; legends="CountryRegionCode";

&nbsp; formatting={

&nbsp;   column: { nameContains:"Revenue", style:"background-color: #f0f0f0; font-weight: bold;" }

&nbsp; }

}}

```



---



\#### \*\*Oszlopdiagram (`barchart`)\*\*

Oszlopdiagramot hoz létre a \[Chart.js](https://www.chartjs.org/) segítségével.



\*\*Specifikus `formatting` opciók:\*\*

\* `title`: A diagram címe.

\* `horizontal`: `true` esetén fekvő oszlopdiagramot rajzol. Alapértelmezett: `false`.

\* `stacked`: `true` esetén halmozott oszlopdiagramot készít. Alapértelmezett: `false`.

\* `borderWidth`: Az oszlopok keretének vastagsága.



\*\*Példa:\*\*

```html

{{

&nbsp; query="SELECT TerritoryName, SalesYTD FROM Sales.vSalesPerson WHERE SalesYTD > 4000000";

&nbsp; representation="barchart";

&nbsp; series="SalesYTD";

&nbsp; legends="TerritoryName";

&nbsp; sortBy="SalesYTD";

&nbsp; sortDirection="desc";

&nbsp; formatting={

&nbsp;   title: "Legjobb területek (Éves eladás)",

&nbsp;   horizontal: true

&nbsp; }

}}

```



---



\#### \*\*Vonaldiagram (`linechart`)\*\*

Vonaldiagramot hoz létre.



\*\*Specifikus `formatting` opciók:\*\*

\* `title`: A diagram címe.

\* `showPoints`: `true` esetén az adatpontokat körökkel jelöli. Alapértelmezett: `true`.

\* `tension`: A vonal görbületét szabályozza (0 = egyenes, 0.4 = enyhén görbült).



\*\*Példa:\*\*

```html

{{

&nbsp; query="SELECT YEAR(OrderDate) as OrderYear, SUM(TotalDue) as TotalRevenue FROM Sales.SalesOrderHeader GROUP BY YEAR(OrderDate) ORDER BY 1";

&nbsp; representation="linechart";

&nbsp; series="TotalRevenue";

&nbsp; legends="OrderYear";

&nbsp; formatting={

&nbsp;   title: "Éves bevételek alakulása",

&nbsp;   tension: 0.4

&nbsp; }

}}

```



---



\#### \*\*Kördiagram (`piechart`)\*\*

Kör- vagy fánkdiagramot hoz létre.



\*\*Specifikus `formatting` opciók:\*\*

\* `title`: A diagram címe.

\* `doughnut`: `true` esetén fánkdiagramot rajzol. Alapértelmezett: `false`.

\* `showValues`: `true` esetén megjeleníti az értékeket.

\* `showPercentages`: `true` esetén megjeleníti a százalékos arányokat.

\* `valuePosition`: Az értékek helye. Lehet `legend` (jelmagyarázatban), `inside` (szeleteken belül) vagy `outside` (szeleteken kívül).



\*\*Példa:\*\*

```html

{{

&nbsp; query="SELECT ShipMethodName, COUNT(\*) as OrderCount FROM Sales.vSalesPerson GROUP BY ShipMethodName";

&nbsp; representation="piechart";

&nbsp; series="OrderCount";

&nbsp; legends="ShipMethodName";

&nbsp; formatting={

&nbsp;   title: "Szállítási módok aránya",

&nbsp;   doughnut: true,

&nbsp;   valuePosition: "inside"

&nbsp; }

}}

```



---



\### 2. Szűrő-komponensek (`filter`)



Ezek a komponensek interaktív vezérlőket (legördülő menük, gombok stb.) hoznak létre, amelyekkel a felhasználó dinamikusan szűrheti a riport adatait. A szűrők a lekérdezésekben lévő `@parameter` változókhoz kapcsolódnak.



\#### \*\*Általános paraméterek (minden szűrőre érvényesek)\*\*



| Paraméter | Típus | Kötelező? | Leírás | Példa |

| :--- | :--- | :--- | :--- | :--- |

| `representation` | string | \*\*Igen\*\* | Értéke mindig `"filter"` kell, hogy legyen. | `representation="filter"` |

| `type` | string | \*\*Igen\*\* | A szűrő típusa. Lásd alább. | `type="dropdown"` |

| `param` | string | \*\*Igen\*\* | Az SQL `@parameter` neve, amit ez a szűrő vezérel. | `param="EvParam"` |

| `label` | string | Nem | A szűrő felett megjelenő címke. | `label="Válassz évet:"` |

| `default` | string | Nem | A szűrő alapértelmezett értéke. | `default="2014"` |



---



\#### \*\*Szűrő típusok (`type`)\*\*



\* \*\*`dropdown`\*\*: Legördülő menü.

&nbsp;   \* \*\*Opciók forrása (az egyik kötelező):\*\*

&nbsp;       \* `dataSource`: SQL lekérdezés, ami a menüpontokat adja.

&nbsp;       \* `options`: Statikus, vesszővel elválasztott lista.

&nbsp;   \* \*\*`dataSource` esetén kötelező:\*\*

&nbsp;       \* `valueField`: Az opciók értékét tartalmazó oszlop.

&nbsp;       \* `textField`: Az opciók szövegét tartalmazó oszlop.

&nbsp;   \* \*\*Példa (dinamikus):\*\*

&nbsp;       ```html

&nbsp;       {{ representation="filter"; type="dropdown"; param="Country"; label="Ország:";

&nbsp;          dataSource="SELECT DISTINCT CountryRegionCode FROM Sales.vSalesPerson";

&nbsp;          valueField="CountryRegionCode"; textField="CountryRegionCode"; }}

&nbsp;       ```

&nbsp;   \* \*\*Példa (statikus):\*\*

&nbsp;       ```html

&nbsp;       {{ representation="filter"; type="dropdown"; param="Status"; label="Státusz:";

&nbsp;          options="Active,Inactive"; default="Active"; }}

&nbsp;       ```



\* \*\*`button`\*\*: Gombcsoport.

&nbsp;   \* `options`: Vesszővel elválasztott lista a gombok értékeiről.

&nbsp;   \* `labels`: Vesszővel elválasztott lista a gombok feliratairól (ha eltér az `options`-től).

&nbsp;   \* \*\*Példa:\*\*

&nbsp;       ```html

&nbsp;       {{ representation="filter"; type="button"; param="ShowAll"; label="Mindent mutat:";

&nbsp;          options="1,0"; labels="Igen,Nem"; default="1"; }}

&nbsp;       ```



\* \*\*`calendar` / `date`\*\*: Dátumválasztó.

&nbsp;   \* \*\*Példa:\*\*

&nbsp;       ```html

&nbsp;       {{ representation="filter"; type="calendar"; param="StartDate"; label="Kezdő dátum:";

&nbsp;          default="2014-01-01"; }}

&nbsp;       ```



\* \*\*`text`\*\*: Szöveges beviteli mező.

&nbsp;   \* \*\*Példa:\*\*

&nbsp;       ```html

&nbsp;       {{ representation="filter"; type="text"; param="NameSearch"; label="Keresés névre:"; }}

&nbsp;       ```



\* \*\*`number`\*\*: Numerikus beviteli mező.

&nbsp;   \* `min`, `max`, `step`: Opcionális HTML5 attribútumok.

&nbsp;   \* \*\*Példa:\*\*

&nbsp;       ```html

&nbsp;       {{ representation="filter"; type="number"; param="MinOrders"; label="Minimum rendelés:";

&nbsp;          min="0"; max="100"; step="5"; default="10"; }}

