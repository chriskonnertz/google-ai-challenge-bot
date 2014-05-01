public class Planet {
    // Initializes a planet.
    public Planet(int planetID,
                  int owner,
		  int numShips,
		  int growthRate,
		  double x,
		  double y) {
	this.planetID = planetID;
	this.owner = owner;
	this.numShips = numShips;
	this.growthRate = growthRate;
	this.x = x;
	this.y = y;

    this.simOwner = 0;
    this.simShips = 0;
    this.availShips = 0;
    this.simOpShips = 0;
    this.timeOfFall=0;
    this.score = 0;
    this.isHead = false;
    this.targetPlanetID = -1;
    }

    // Initializes a planet.
    public Planet(int planetID,
                  int owner,
          int numShips,
          int growthRate,
          double x,
          double y,
          int availShips,
          bool isHead,
          int targetPlanetID)
    {
        this.planetID = planetID;
        this.owner = owner;
        this.numShips = numShips;
        this.growthRate = growthRate;
        this.x = x;
        this.y = y;

        this.simOwner = 0;
        this.simShips = 0;
        this.availShips = availShips;
        this.simOpShips = 0;
        this.timeOfFall = 0;
        this.score = 0;
        this.isHead = isHead;
        this.targetPlanetID = targetPlanetID;
    }

    // Accessors and simple modification functions. These should be mostly
    // self-explanatory.
    public int PlanetID() {
	return planetID;
    }

    public int Owner() {
	return owner;
    }

    public int NumShips() {
	return numShips;
    }

    public int GrowthRate() {
	return growthRate;
    }

    public double X() {
	return x;
    }

    public double Y() {
	return y;
    }

    public double Score()
    {
        return score;
    }

    public int SimShips()
    {
        return simShips;
    }

    public int SimOwner()
    {
        return simOwner;
    }

    public int AvailShips()
    {
        return availShips;
    }

    public int SimOpShips()
    {
        return simOpShips;
    }

    public int TimeOfFall()
    {
        return timeOfFall;
    }

    public bool IsHead()
    {
        return isHead;
    }

    public int TargetPlanetID()
    {
        return targetPlanetID;
    }

    public void TargetPlanetID(int targetPlanetID)
    {
        this.targetPlanetID = targetPlanetID;
    }

    public void IsHead(bool isHead)
    {
        this.isHead = isHead;
    }

    public void Score(double value)
    {
        this.score = value;
    }

    public void AvailShips(int amount)
    {
        this.availShips = amount;
    }

    public void SimShips(int amount)
    {
        this.simShips = amount;
    }

    public void SimOwner(int value)
    {
        this.simOwner = value;
    }

    public void SimOpShips(int amount)
    {
        this.simOpShips = amount;
    }

    public void TimeOfFall(int turns)
    {
        this.timeOfFall = turns;
    }

    public void Owner(int newOwner) {
	this.owner = newOwner;
    }

    public void NumShips(int newNumShips) {
	this.numShips = newNumShips;
    }

    public void AddShips(int amount) {
	numShips += amount;
    }

    public void RemoveShips(int amount) {
	numShips -= amount;
    }

    private int planetID;
    private int owner;
    private int numShips;
    private int growthRate;
    private double x, y;
    private int simShips;
    private int simOwner;
    private int availShips;
    private int timeOfFall;
    private int simOpShips;
    private double score;
    private bool isHead;
    private int targetPlanetID;

    private Planet (Planet _p) {
	planetID = _p.planetID;
	owner = _p.owner;
	numShips = _p.numShips;
	growthRate = _p.growthRate;
	x = _p.x;
	y = _p.y;
    simShips = _p.simShips;
    simOwner = _p.simOwner;
    availShips = _p.availShips;
    timeOfFall = _p.timeOfFall;
    simOpShips = _p.simOpShips;
    score = _p.score;
    isHead = _p.isHead;
    targetPlanetID = _p.targetPlanetID;
    }
}
