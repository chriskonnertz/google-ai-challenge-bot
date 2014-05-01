using System;
using System.Collections.Generic;

public class MyBot {
    // © 2010 source by siconize
    // Version: 0.7.0

    public struct sScoredPlanet
    {
        public Planet myPlanet;
        public double score;
    }

    public static double MyPlanetCount = 0;
    public static double OpPlanetCount = 0;
    public static int turn = 0;
    public static List<Planet> Expansions = null; // Planeten, zu denen gerade Schiffe von uns unterwegs sind, um zu expandieren
    public static List<Planet> MyPlanetsAll = null; // pw.MyPlanet() + Expansions

    public static void DoTurn(PlanetWars pw)
    {
        DateTime StartTime = DateTime.Now; // Startzeit festhalten, um herauszufinden, wie lange der Zug des Bots andauert

        //List<Fleet> ShadowFleets = pw.MyFleets(); // "ShadowFleets" sind alle unsere Flotten + die in dieser Runde von uns losgeschicken Flotten. (Letztere sind nicht teil von pw.MyFleets()!)

        turn++;
        //log("turn: " + turn.ToString());
        if (pw.Winner() != -1) return; // spiel ist vorbei, abbruch.

        // Start-Expanison ------------------------------------------------------------------------------------------------------------------
        // Wir benutzen knapsack01. Achtung: Diese Vorgehensweise beduetet nicht notwendiger Weise eine optimale Expansion!
        if (turn == 1)
        {
            Planet my_start = pw.MyPlanets()[0];
            Planet enemy_start = pw.EnemyPlanets()[0];

            // Schritt 1: Wie viele Schiffe kann ich schicken, ohne dass mein Hauptplanet eingenommen werden kann?
            int ships_available = Math.Min(my_start.NumShips(), my_start.GrowthRate() * ((int)distance(my_start, enemy_start)+1));

            // Schritt 2: Planet heraus suchen, die fuer eine Expansion grundsaetzlich geeignet sind
            List<Planet> candidates = new List<Planet>();
            foreach (Planet p in pw.NeutralPlanets())
            {
                if (Math.Ceiling(distance(p, my_start)) <= Math.Ceiling(distance(p, enemy_start)))
                    candidates.Add(p);
            }

            // Schritt 3: Suche die besten Planeten zur Expansion heraus
            Expansions = knapsack01(candidates, ships_available, my_start);

            // Schritt 4: Expansion starten
            foreach (Planet p in Expansions)
            {
                SendShips(pw, my_start, p, p.NumShips() + 1);
            }

            return;
        }

        int MyGain = 0; // Gesamtproduktionsrate aller (meiner) Planeten
        int OpGain = 0;
        int MyShipCount = pw.NumShips(1); // Gesamtzahl aller meiner Schfife (auf Planeten und unterwegs)
        int OpShipCount = pw.NumShips(2);
        int MyScore = 0; // Meine Bewertung
        bool AttackIsEnabled = true; // duerfen wir in dieser Runde angreifen?

        // Statistiken fuer beide Spieler erheben ------------------------------------------------------------------------------------------------------------------
        MyPlanetCount = 0; OpPlanetCount = 0;
        foreach (Planet p in pw.Planets())
        {
            if (p.Owner() == 1)
            {
                MyGain += p.GrowthRate();
                MyPlanetCount++;
            }
            if (p.Owner() == 2)
            {
                OpGain += p.GrowthRate();
                OpPlanetCount++;
            }
        }

        MyScore = (MyShipCount + MyGain) - (OpShipCount + OpGain);

        MyPlanetsAll = pw.MyPlanets(); // MyPlanetsAll := pw.MyPlanet() + Expansions
        foreach (Planet p in MyPlanetsAll) { // Planeten, auf die wi expandieren wollten, und die nun uns gehoeren, aus der Liste der Expansionsplaneten entfernen
            foreach (Planet ep in Expansions) {
                if (p.PlanetID() == ep.PlanetID())
                {
                    Expansions.Remove(ep);
                    break;
                }
            }
        }
        MyPlanetsAll.AddRange(Expansions);

        // Front-Planeten finden ------------------------------------------------------------------------------------------------------------------
        if (OpPlanetCount > 0 && MyPlanetCount > 0)
        {
            // Haupt-Planeten finden
            foreach (Planet p in MyPlanetsAll)
            {
                double minDist = Double.MaxValue;
                double dist = 0;
                double opDist = 0;
                Planet e = null;
                Planet q = null;

                // naehesten Planeten des Gegners finden: e
                foreach (Planet opp in pw.EnemyPlanets())
                {
                    dist = distance(p, opp);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        e = opp;
                    }
                }
                opDist = minDist;

                // den eigenen Planeten finden, der e am naehsten ist: q
                if (MyPlanetsAll.Count > 1)
                {
                    minDist = Double.MaxValue;
                    foreach (Planet myp in MyPlanetsAll)
                    {
                        dist = distance(e, myp);
                        if ((dist < minDist) && (distance(p, myp) <= opDist))
                        {
                            minDist = dist;
                            q = myp;
                        }
                    }
                }
                else q = p;

                if (p == q)
                {
                    p.IsHead(true);
                    p.TargetPlanetID(e.PlanetID());
                }
                else
                {
                    p.IsHead(false);
                    p.TargetPlanetID(q.PlanetID());
                }
            }
        }

        // Verteidigung ------------------------------------------------------------------------------------------------------------------
        int maxturns = 0;
        foreach (Fleet f in pw.Fleets())
        {
            if (f.TurnsRemaining() > maxturns) maxturns = f.TurnsRemaining();
        }

        foreach (Planet p in MyPlanetsAll)
        {
            p.SimOwner(p.Owner()); p.SimShips(p.NumShips()); p.AvailShips(p.NumShips()); p.TimeOfFall(-1);
        }

        // Berechne, wie viele Schiffe jeder Planet schicken kann, ohne vom Gegner uebernommen zu werden.
        // Dabei wird auch erkannt, welche Planeten vom Gegner erobert werden, sofern sie keine Verstaerkung erhalten.
        for (int i = 1; i <= maxturns; i++)
        {
            foreach (Planet p in MyPlanetsAll)
            {
                if (p.SimOwner() != 0) p.SimShips(p.SimShips() + p.GrowthRate()); // Schiffproduktion berechnen

                foreach (Fleet f in pw.Fleets())
                {
                    if (f.DestinationPlanet() == p.PlanetID() && f.TurnsRemaining() == i)
                    {
                        // achtung: reihenfolge der folgenden if-abfragen ist relevant!
                        if (p.SimOwner() == f.Owner())
                        {
                            p.SimShips(p.SimShips() + f.NumShips()); // eigener Planet (aus Sicht der Flotte)
                            //if (p.SimOwner() == 2) p.SimOpShips(p.SimOpShips() + f.NumShips());
                        }
                        if (p.SimOwner() != f.Owner()) // neutraler/gegnerischer Planet (aus Sicht der Flotte)
                        {
                            p.SimShips(p.SimShips() - f.NumShips());
                            if (p.SimShips() < 0) p.SimOwner(f.Owner());
                            p.SimShips(Math.Abs(p.SimShips()));
                            if (p.SimOwner() == 2)
                            {
                                p.TimeOfFall(i); // speichern, wann der Planet vom Gegner erobert wurde (eigene Rueckeroberungen interessieren hier nicht)
                                p.SimOpShips(p.SimShips());
                                p.AvailShips(0); // Der Planet wurde zwischenzeitlich erobert. Moeglicherweise wird er spaeter zurueck erobert, aber trotzdem soll er in dieser Runde erst einmal keine Schfife schicken. Tendenziell kann dadurch die Zeit, bis der Planet zurueckerobert wird, sinken, was mehr Produktion fuer uns bedeutet.
                            }
                            else p.TimeOfFall(-1);
                        }
                    }
                }

                if (p.Owner() == 1) p.AvailShips(Math.Min(p.AvailShips(), p.SimShips()));
            }
        }

        // Listen erstellen der Planeten, die erobert werden bzw. Verstaerkung schicken koennen
        List<Planet> defplanets = new List<Planet>();
        List<Planet> fallenplanets = new List<Planet>();
        foreach (Planet p in MyPlanetsAll)
        {
            if (p.TimeOfFall() != -1) // Planeten, die ohne zusaetzliche Verstaerkung dauerhaft erobert wuerden
            {
                fallenplanets.Add(p);
                fallenplanets[fallenplanets.Count - 1].Score(p.GrowthRate());
                p.AvailShips(0); // der Planet hat keine Schiffe, die er als Verstaerkung schicken kann, ohne erobert zu werden, da er (voraussichtlich) so oder so erobert wird.
            }
            if (p.TimeOfFall() == -1 && p.AvailShips() > 0 && p.Owner() == 1) defplanets.Add(p); // Planeten, die ohne zusaetliche Verstaerkung nicht erobert werden (oder aber wieder zurueck erobert werden) und evtl. Schiffe schicken koennen
        }
        fallenplanets.Sort(comparePlanetListDesc); // nach Prioritaet sortieren

        // Zu den bedrohten Planeten Verstaerkung schicken
        foreach (Planet fallenplanet in fallenplanets)
        {
            foreach (Planet p in defplanets) // Fuer alle Planeten, die Verstaerkung schicken koennen/sollen...
            {
                p.Score((int)Math.Ceiling(distance(fallenplanet, p))); // ...Entfernung zum Zielplaneten ermitteln
            }
            defplanets.Sort(comparePlanetListAsc); // Wir wollen zuerst von nahe gelegenen Planeten Unterstuetzung senden.

            int sended = 0; // Gibt an, wie viele Schiffe andere Planeten, die naeher am Zielplaneten sind, schon gesendet haben
            int iMustSend = 0; // Anzahl Schiffe, die der Planet senden muss (ob er so viele Schiffe hat, wird hier nicht beruecksichtigt)

            foreach (Planet p in defplanets)
            {
                if (p.AvailShips() == 0) continue; // Planeten, die keine Schiffe schicken koennen, ignorieren
                if (p.Score() <= fallenplanet.TimeOfFall()) // p.score()=Entfernung=Zeit, bis Flotten den bedrohten Planet erreichen koennen
                {
                    iMustSend = fallenplanet.SimOpShips() + 1 - sended; // Ich befinde mich nah genug beim Zielplaneten, um eingreifen zu koennen, bevor er erobert wird. SimOpShips() gibt an, wie viele Schiffe der Planet direkt nach der Eroberung hat. Wenn der Planet so viele Schiffe mehr schickt, wird die Eroberung verhindert. [Es kann sein, dass der Planet zu viele Schiffe schickt, wenn z.B. der Planet vor der endgueltigen Eroberung schon mal  kurz erobert wurde und wir das verhinderen, denn dann werden ja mehr Schfife fuer uns produziert.] 
                }
                else
                {
                    iMustSend = fallenplanet.SimOpShips() + 1 + (int)(p.Score() - fallenplanet.TimeOfFall()) * fallenplanet.GrowthRate() - sended; // wie oben, aber jetzt beruecksichtigt der Planet zusaetlich, dass seine Flotte er erst nach der Eroberung eintrifft und der Gegner dann schon zusaetzliche Schiffe produziert hat. Es wird aber nicht beruecksichtigt, dass der Gegner ggf. Verstaerkung zu dem Planeten schickt.
                }

                if (p.AvailShips() >= iMustSend)
                {
                    p.AvailShips(p.AvailShips() - iMustSend);
                    SendShips(pw, p, fallenplanet, iMustSend);
                    break; // Es wurden (voraussichtlich) genug Schiffe gesendet, um den Planeten zurueck zu erobern. Bearbeitung dies Planeten abbrechen.
                }
                else
                {
                    sended += p.AvailShips();
                    SendShips(pw, p, fallenplanet, p.AvailShips());
                    p.AvailShips(0);
                }
            }
        }

        

        //// Expansion ------------------------------------------------------------------------------------------------------------------
        if (MyPlanetCount > 0 && OpPlanetCount > 0 && (MyScore < 150 && (MyGain - 5 < OpGain)))
        //if (MyPlanetCount > 0 && OpPlanetCount > 0 && (MyScore < 250 && (MyGain - 20 < OpGain)))
        {
            // Suche einen geeigneten Planeten zur Expansion:
            Planet ExpansionPlanet = null;
            Planet MyNearestPlanet = null;
            double maxScore = Double.MaxValue;
            foreach (Planet p in pw.NeutralPlanets())
            {
                if (p.GrowthRate() == 0) continue; // planeten ohne produktion interessieren uns nicht

                // pruefen, ob wir noch nicht zu diesem Planeten expandieren
                bool gofornext = false;
                foreach (Planet ExP in Expansions)
                {
                    if (p.PlanetID() == ExP.PlanetID())
                    {
                        p.Score(-1);
                        gofornext = true;
                        break;
                    }
                }
                if (gofornext == true) continue;

                p.Score(p.NumShips() / p.GrowthRate()); // p.GrowthRate() == 0 kann hier nicht auftreten

                // Der Planet sollte/muss in unserem Gebiet liegen oder zumindest fuer uns guenstiger als fuer den Gegner
                int MyEmpireDistance = int.MaxValue;
                int OpEmpireDistance = int.MaxValue;
                Planet SaveMyNearestPlanet = null;
                foreach (Planet MyP in pw.MyPlanets())
                {
                    int mindDist = (int)Math.Ceiling(distance(p, MyP));
                    if (mindDist < MyEmpireDistance)
                    {
                        MyEmpireDistance = mindDist;
                        SaveMyNearestPlanet = MyP;
                    }
                }
                foreach (Planet OpP in pw.EnemyPlanets())
                {
                    int mindDist = (int)Math.Ceiling(distance(p, OpP));
                    if (mindDist < OpEmpireDistance) OpEmpireDistance = mindDist;
                }

                // Entfernungen mit in die Bewertung einbeziehen
                //if (OpEmpireDistance < MyEmpireDistance) p.Score(p.Score() + 100 + (MyEmpireDistance - OpEmpireDistance)); // Wir wollen Planeten, die naeher zum Gegner sind also zu uns, nur im Notfall. Daher erhalten sie eine dratsich schlechtere Wertung.
                if (OpEmpireDistance < MyEmpireDistance) continue; // Wir wollen Planeten, die naeher zum Gegner sind also zu uns, nicht einnehmen.
                p.Score(p.Score() + MyEmpireDistance);

                if (p.Score() < maxScore)
                {
                    maxScore = p.Score();
                    ExpansionPlanet = p;
                    MyNearestPlanet = SaveMyNearestPlanet;
                }
            }

            log(" "); log("turn: " + turn);
            log("MyGain: " + MyGain + ", OpGain: " + OpGain + ", Score: " + MyScore);
            //if (ExpansionPlanet == null) log("Kein Expansionplanet gefunden."); else log("Expansionplanet gefunden!!!  ID: " + ExpansionPlanet.PlanetID() + " - Score: " + ExpansionPlanet.Score());

            if (ExpansionPlanet != null) // kein fuer Expansion geeigneter Planet gefunden: Abbruch
            {
                // Berechne via AvailShips, ob wir genuegend Schfife haben, um die Eroberung des Planeten in dieser Runde einzuleiten.
                int haveShips = 0;
                foreach (Planet p in pw.MyPlanets())
                {
                    haveShips += p.AvailShips();
                    if (haveShips >= ExpansionPlanet.NumShips() + 1) break;
                }

                bool Expand = true;
                if (haveShips>0) { // Wenn wir genug Schiffe fuer die Expansion haben:
                    int amortizeTime = ExpansionPlanet.NumShips() / ExpansionPlanet.GrowthRate() + (int)Math.Ceiling(distance(MyNearestPlanet, ExpansionPlanet));
                    int needed = ExpansionPlanet.NumShips() + 1;
                    log("amortizeTime: " + amortizeTime + ", MyNearestPlanet: " + MyNearestPlanet.PlanetID() + ", haveShips: " + haveShips + ", neededShips: " + needed);

                    foreach (Planet HeadPlanet in MyPlanetsAll) // Angriff auf jeden HeadPlaneten simulieren; auch Expansionsplaneten beruecksichtigen
                    {
                        if (HeadPlanet.IsHead() == true)
                        {
                            int SimShips = 0;
                            int IsTarget = 0;
                            List<Fleet> ESimFleets = new List<Fleet>(); // Simulierte Flotten hier speichern
                            foreach (Planet p in pw.Planets()) // Alle nicht-neutralen Planeten greifen an (bzw. verstaerken)
                            {
                                if (p.Owner() != 0)
                                {
                                    for (int i = 0; i <= amortizeTime; i++)
                                    {
                                        SimShips = p.GrowthRate(); // Die Schiff-Produktion beruecksichtigen
                                        if (i == 0 && p.PlanetID() != HeadPlanet.PlanetID()) SimShips = p.NumShips(); // Alle Schiffe losschicken
                                        if (p.PlanetID() == HeadPlanet.PlanetID()) IsTarget = 1; else IsTarget=0; // Flotte markieren: Diese Flotte wurde vom Zielplaneten losgeschickt (sie fliegt also eigentlich gar nicht. Sie wird nur losgeschickt, damit die Produktion des Ziel-Planeten beruecksichtigt wird)
                                        if (SimShips > 0) // wir wollen keine 0-Schiff-Flotten losschicken
                                        {
                                            Fleet ThisFleet = new Fleet(p.Owner(), SimShips, IsTarget, 0, -1, (int)Math.Ceiling(distance(p, HeadPlanet)));
                                            ESimFleets.Add(ThisFleet); // Flotte der Liste hinzufuegen
                                        }
                                    }
                                }
                            }

                            ESimFleets.Sort(compareFleetListAsc); // Flotten nach Ankunftszeit sortieren

                            SimShips = HeadPlanet.NumShips() - needed; // Der Zielplanet verfuegt ueber die auf ihm stationierten Schiffe. Die Schiffe fuer die Expansion werden abgezogen.
                            int SimOwner = 1; // Besitzer des Zielplaneten
                            foreach (Fleet ESimFleet in ESimFleets) // Eintreffen der Flotten simulieren. Da diese nach Entfernung sortiert sind, muss die exakte Akunftszeit hier nicht mehr beruecksichtigt werden.
                            {
                                if (ESimFleet.Owner() == SimOwner || ESimFleet.SourcePlanet() == 1) // Wenn der Zielplnaet zum Besitzer der Flotte gehoert (oder die Flotte als zum Zielplanet gehoerend markiert wurde):
                                {
                                    SimShips += ESimFleet.NumShips(); // Schiffe addieren
                                }
                                else
                                {
                                    SimShips -= ESimFleet.NumShips(); // Schiffe abziehen
                                    if (SimShips < 0) // Wenn wir < 0 Schiffe haben...
                                    {
                                        SimOwner = ESimFleet.Owner(); // wechselt der Besitzer des Planeten.
                                        SimShips = Math.Abs(SimShips); 
                                    }
                                }
                            }

                            log("Ergebnis der Simulation: " + SimShips + " (" + SimOwner + ")");
                            if (SimOwner != 1) // Wenn wir nicht der Besitzer des Planeten sind...
                            {
                                Expand = false; // Expansion abbrechen, da keine sichere Expansion moeglich ist. 
                                break; // Schleife ueber die Head-Planeten verlassen
                            }
                        }
                    }

                    if ((haveShips >= needed) && (Expand == true))
                    {
                        List<Planet> SourcePlanets = EvalSendPlanets(pw, ExpansionPlanet); // Liste aller infrage kommender eigener Planeten, die Flotten schicken koennen, erstellen. Fuer jeden Planeten eine Bewertung mitliefern.
                        SourcePlanets.Sort(comparePlanetListDesc); // Liste sortieren (nach Bewertung)

                        foreach (Planet SourcePlanet in SourcePlanets) // Alle moeglichen eigenen Planeten durchlaufen, die zum aktuellen Zielplaneten Flotten schicken koennen
                        {
                            int ships = SourcePlanet.AvailShips();
                            if (ships == 0) continue; // wir wollen ja keine 0-Schiff-Flotten losschicken
                            if (ships >= needed) // Wenn der Planet mehr Schiffe hat als fuer die Expansion benoetigt werden:
                            {
                                SourcePlanet.AvailShips(SourcePlanet.AvailShips() - needed);
                                SendShips(pw, SourcePlanet, ExpansionPlanet, needed);
                                break; // Abbruch, es wurden genug Schiffe losgeschickt
                            }
                            else
                            {
                                SourcePlanet.AvailShips(0);
                                SendShips(pw, SourcePlanet, ExpansionPlanet, ships);
                                needed -= ships;
                            }
                        }

                        Expansions.Add(ExpansionPlanet); // Planet zu der Liste der Expansionsplaneten hinzufuegen
                    }
                    else
                    {
                        if (haveShips < needed && Expand == true) AttackIsEnabled = false; // keine Angriffe durchfuehren, sondern warten, dass genug Schiffe produziert worden
                    }
                }
            }
        }
        
        // Nachschublinien ------------------------------------------------------------------------------------------------------------------
        if (OpPlanetCount > 0 && MyPlanetsAll.Count > 1) // Nachschub schicken, wenn der Gegner Planeten besitzt und wir mehr als 1 Planeten besitzen:
        {
            foreach (Planet p in pw.MyPlanets())
            {
                //if (p.AvailShips() > 0 && p.IsHead() == false && p.Owner() == 1)
                if (p.AvailShips() > 0 && p.IsHead() == false) // Nachschub schicken, wenn dafuer Schiffe verfuegbar sind und wir kein HEad-Planet sind.
                {
                    SendShips(pw, p, pw.GetPlanet(p.TargetPlanetID()), p.AvailShips());
                }
            }
        }

        // Angriff ------------------------------------------------------------------------------------------------------------------
        //log("Attack? : " + AttackIsEnabled + ", AvaiLShips(2): " + pw.GetPlanet(2).AvailShips() + ", OpPCount: " + OpPlanetCount + ", MyPCount: " + MyPlanetCount);
        if (OpPlanetCount > 0 && MyPlanetCount > 0 && AttackIsEnabled == true)
        {
            foreach (Planet p in pw.MyPlanets())
            {
                if (p.AvailShips() > 0 && p.IsHead() == true) // Angreifen, wenn wir Schiffe dazu haben und wir ein Head-Planet sind:
                {
                    int ships_available = p.AvailShips() + p.GrowthRate() * ((int)distance(p, pw.GetPlanet(p.TargetPlanetID())));
                    if (pw.GetPlanet(p.TargetPlanetID()).NumShips() <= ships_available || MyScore > 200)
                    {
                        if (MyScore > 250) // Wenn wir ueber x Schiffe mehr haben als der Gegner, greifen wir aggressiv an. 
                        {
                            ships_available = p.AvailShips();
                        }
                        else
                        {
                            ships_available = Math.Min(ships_available - pw.GetPlanet(p.TargetPlanetID()).NumShips(), p.AvailShips());
                        }
                        if (ships_available > 0) SendShips(pw, p, pw.GetPlanet(p.TargetPlanetID()), ships_available);
                    }
                }
            }
        }

        log("ExecutionTime: " + (DateTime.Now - StartTime).TotalMilliseconds + " Milliseconds (" + (DateTime.Now - StartTime).TotalSeconds + " Seconds)"); // Ausfuehrungszeit loggen.
    }

    // Knapsack-Kalkulation.
    // planets ist eine Liste von Planeten, die beruecksichtigt werden sollen. 
    // maxWeight gibt in diesem Fall die maximale Anzahl von Schiffen an.
    // Der Rueckgabewert dieser Funktion ist eine Liste von "optimalen" Planeten.
    // Siehe auch: http://de.wikipedia.org/wiki/Rucksackproblem
    public static List<Planet> knapsack01(List<Planet> planets, int maxWeight, Planet source)
    {
        List<int> weights = new List<int>();
        List<int> values = new List<int>();
        // solve 0-1 knapsack problem 
        foreach (Planet p in planets)
        {
            // here weights and values are numShips and growthRate respectively 
            // you can change this to something more complex if you like...
            weights.Add(p.NumShips() + 1);
            //values.Add(p.GrowthRate());
            //values.Add(p.GrowthRate()*100-p.NumShips()); // schiff-kosten beruecksichtigen
            values.Add(p.GrowthRate() * 100 - (int) distance(p, source)); // entfernung beruecksichtigen
        }
        // Move this line here since weights-count was unknown until now.
        int[,] K = new int[weights.Count + 1, maxWeight];

        for (int i = 0; i < maxWeight; i++)
        {
            K[0, i] = 0;
        }
        for (int k = 1; k <= weights.Count; k++)
        {
            for (int y = 1; y <= maxWeight; y++)
            {
                if (y < weights[k - 1])
                {
                    K[k, y - 1] = K[k - 1, y - 1];
                }
                else if (y > weights[k - 1])
                {
                    K[k, y - 1] = Math.Max(K[k - 1, y - 1], K[k - 1, y - 1 - weights[k - 1]] + values[k - 1]);
                }
                else
                    K[k, y - 1] = Math.Max(K[k - 1, y - 1], values[k - 1]);
            }
        }

        // get the planets in the solution
        int idx = weights.Count;
        int currentW = maxWeight - 1;
        List<Planet> markedPlanets = new List<Planet>();
        while ((idx > 0) && (currentW >= 0))
        {
            if (((idx == 0) && (K[idx, currentW] > 0)) || (K[idx, currentW] != K[idx - 1, currentW]))
            {
                markedPlanets.Add(planets[idx - 1]);
                currentW = currentW - weights[idx - 1];
            }
            idx--;
        }
        return markedPlanets;
    }

    // Sendet, sofern moeglich, eine bestimmte Anzahl Schiffe von einem Planet.
    // Der Planet muss unter usnerer Kontrolle stehen.
    // Gibt true zurueck, wenn so viele Schiffe wie angefordert wurden vorhanden waren.
    public static Boolean SendShips(PlanetWars pw, Planet sourcePlanet, Planet destPlanet, int neededShips)
    {
        int myships = sourcePlanet.NumShips();
        if (myships > 0)
        {
            if (myships >= neededShips)
            {
                pw.IssueOrder(sourcePlanet, destPlanet, neededShips); // Befehl ausfuehren
                return true;
            }
            else
            {
                pw.IssueOrder(sourcePlanet, destPlanet, myships); // Befehl ausfuehren
            }
        }
        return false;
    }

    // Berechnet den Status eines Planeten in der Zukunft.
    // turns gibt den Zeitpunkt in Iterationen an, der vorausberechnet werden soll.
    // Ergebnis sind Angaben ueber den Besitzer und die Anzahl der stationierten Schiffe.
    // Diese werden als Referenz uebergeben.
    //
    // ToDo: Den Fall beruecksichtigen, dass zwei gleich groﬂe Flotten auf einen Neutralen Planeten treffen (sie neutralisieren sich)
    //
    // !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
    // ANMERKUNG: Funktion wird derzeit nicht benutzt. Falls doch, diesen Kommentar entfernen!
    // !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
    //
    public static int CalcPlanetFuture(PlanetWars pw, Planet p, int turns, ref int owner, ref int ships)
    {
        ships = p.NumShips();
        owner = p.Owner();
        List<Fleet> SimFleets = pw.Fleets();

        for (int i = 1; i <= turns; i++) {
            if (owner != 0) ships += p.GrowthRate();  // Schiffproduktion berechnen

            foreach (Fleet f in SimFleets)
            {
                if (f.TurnsRemaining() == i && f.DestinationPlanet()==p.PlanetID())
                {
                    // achtung: reihenfolge der folgenden if-abfragen ist relevant!
                    if (owner == f.Owner()) ships += f.NumShips(); // eigener Planet (aus Sicht der Flotte)
                    if (owner != f.Owner() && owner != 0) // neutraler/gegnerischer Planet (aus Sicht der Flotte)
                    {
                        ships -= f.NumShips();
                        if (ships < 0) owner = f.Owner();
                        ships = Math.Abs(ships);
                    }
                }
            }
        }

        return ships;
    }

    // Liste aller infrage kommender Expansions-Planeten erstellen.
    // Fuer jeden Planeten eine Bewertung mitliefern.
    //
    // !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
    // ANMERKUNG: Funktion wird derzeit nicht benutzt. Falls doch, diesen Kommentar entfernen!
    // !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
    //
    public static List<Planet> EvalExPlanets(PlanetWars pw)
    {
        List<Planet> ExPlanets = new List<Planet>();

        //foreach (Planet p in pw.Planets())
        foreach (Planet p in pw.NeutralPlanets())
        {
            if (p.Owner() != 1) // eigene Planeten ignorieren
            {
                double fleets = p.NumShips(); // Anzahl stationierter Flotten
                double gain = p.GrowthRate(); // Baurate
                //double myDist = distance(MyEmpireCenterX, MyEmpireCenterY, p.X(), p.Y());
                double opDist = 1;
                //if (OpPlanetCount > 0) distance(OpEmpireCenterX, OpEmpireCenterY, p.X(), p.Y());

                double score = (gain / (fleets+1)) / opDist; // Bewertungsfunktion (+1, damit es keine Division durch 0 geben kann)

                Planet ThisPlanet = p;
                ThisPlanet.Score(score);
                ExPlanets.Add(ThisPlanet);
            }
        }

        return ExPlanets;
    }

    // Liste aller eigener Planeten erstellen, die Flotten senden koennen.
    // Fuer jeden Planeten eine Bewertung (Entfernung zum Zielplaneten) mitliefern.
    public static List<Planet> EvalSendPlanets(PlanetWars pw, Planet DestinationPlanet)
    {
        List<Planet> SendPlanets = new List<Planet>();

        foreach (Planet p in pw.MyPlanets())
        {
            if (p.NumShips() > 0)
            {
                double dist = distance(p, DestinationPlanet); // Entfernung zum Zielplaneten ermitteln

                Planet ThisPlanet = p;
                ThisPlanet.Score(dist);
                SendPlanets.Add(ThisPlanet);
            }
        }

        return SendPlanets;
    }

    // Sortiert eine Liste von Planeten mit Bewertungen
    // Reihenfolge: Aufsteigend
    // Anwedungsbeispiel: ExPlanets.Sort(comparePlanetList)
    public static int comparePlanetListAsc (Planet first, Planet second)
    {
        if (first.Score() == second.Score()) return 0;
        if (first.Score() > second.Score()) return 1; else return -1;
    }

    // Sortiert eine Liste von Planeten mit Bewertungen
    // Reihenfolge: Absteigend
    // Anwedungsbeispiel: ExPlanets.Sort(comparePlanetList)
    public static int comparePlanetListDesc(Planet first, Planet second)
    {
        if (first.Score() == second.Score()) return 0;
        if (first.Score() > second.Score()) return -1; else return 1;
    }

    // Sortiert eine Liste von Flotten nach "verbleibenden Turns"
    // Reihenfolge: Aufsteigend
    // Anwedungsbeispiel: SimFleets.Sort(compareFleetListAsc)
    public static int compareFleetListAsc(Fleet first, Fleet second)
    {
        if (first.TurnsRemaining() == second.TurnsRemaining()) return 0;
        if (first.TurnsRemaining() > second.TurnsRemaining()) return 1; else return -1;
    }

    // Hilfsfunktion.
    // Gibt die Entfernung zwischen zwei Punkten zureuck und damit auch, wie lange Flotten zwischen ihnen unterwegs sind.
    static double distance(double x1, double y1, double x2, double y2)
    {
        x1 = x1-x2;
        y1 = y1-y2;
        return Math.Sqrt(x1 * x1 + y1 * y1);
    }

    // Hilfsfunktion.
    // Gibt die Entfernung zwischen zwei Planeten zureuck und damit auch, wie lange Flotten zwischen ihnen unterwegs sind.
    static double distance(Planet source, Planet destination)
    {
        double dx = source.X() - destination.X();
        double dy = source.Y() - destination.Y();
        return Math.Sqrt(dx * dx + dy * dy);
    }

    // Log-Funktion
    static void log(string text)
    {
        WriteFileAppend(@"C:\Users\siconize\Desktop\log.txt", text + "\r\n"); // "@" ist noetig, weil sonst "\" als Sonderzeichen fuer z.B. "\n" interpretiert wird
    }

    // Schreibt Text in eine Datei (Append-Modus)
    static void WriteFileAppend(string sFilename, string sLines)
    {
        System.IO.StreamWriter myFile = new System.IO.StreamWriter(sFilename, true, System.Text.Encoding.UTF8);
        myFile.Write(sLines);
        myFile.Close();
    }
    
    public static void Main() {
	    string line = "";
	    string message = "";
	    int c;
	    try {
	        while ((c = Console.Read()) >= 0) 
            {
		        switch (c) {
		        case '\n':
		            if (line.Equals("go")) {
			            PlanetWars pw = new PlanetWars(message);
			            DoTurn(pw);
		                pw.FinishTurn();
			            message = "";
		            } else {
			            message += line + "\n";
		            }
		            line = "";
		            break;
		        default:
		            line += (char)c;
		            break;
		        }
	        }
	    } catch (Exception) {
	        // Owned.
	    }
    }
}

