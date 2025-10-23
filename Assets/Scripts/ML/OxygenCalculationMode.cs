/**
Defines how to calculate oxygen from multiple runs in CSV
*/
public enum OxygenCalculationMode
{
    // average of all runs
    Average,

    // last run only
    LastRun,

    // first run only
    FirstRun,

    // minimum oxygen (worst case)
    Minimum,

    // maximum oxygen (best case)
    Maximum,

    // median value
    Median,

    // use specific column by name 
    SpecificColumn
}

