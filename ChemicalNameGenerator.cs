using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

// Quick and dirty replacement to the "Utils" methods which belonged to a script I've 
// long since deleted.
public static class ChemicalGenUtils
{
    // THIS WILL THROW AN ERROR FOR ROMAN NUMERALS > 10
    // For now atleast, this script probably won't need to go above 10. Just keep this in mind.
    public static string[] RomanNumerals = {"I","II","III","IV","V","VI","VII","VIII","IX","X"}; 

    // Unicode doesn't support some superscript characters (1, 2, and 3 mainly)
    // Because of this, the function just returns the "power to" symbol along with the number.
    // Feel free to change this method to whatever you want. It's just here as a sort of placeholder almost.
    public static string Superscript(int numberRaw)
    {
        return "^"+numberRaw.ToString();
    }
}

[Serializable]
public class BaseChemicalElement
{
    protected static Dictionary<int, string> prefixes = new Dictionary<int, string>()
    {
        { 1, "mono" },
        { 2, "di" },
        { 3, "tri" },
        { 4, "tetra" },
        { 5, "penta" },
        { 6, "hexa" },
        { 7, "hepta" },
        { 8, "octo" },
        { 9, "nona" },
        { 10, "deca" }
    };

    public string name { get; private set; }
    public string symbol { get; private set; }

    public BaseChemicalElement(string name, string symbol)
    {
        this.name = name;
        this.symbol = symbol;
    }

    protected static string ElementNameToIon(string name, string prefix = "", bool changeEnding = true)
    {
        //Execute code if there is a prefix to use
        if (!string.IsNullOrEmpty(prefix))
        {
            //Removes the prefix vowel so the element name beginning and prefix ending don't have 2 consecutive vowels
            //  (It would sound rather weird to have a compound called "Sulfur HeptaOxide" right?)
            char prefixEnd = prefix.Last();
            string vowels = "aeiou";
            if (vowels.Contains(prefixEnd) && vowels.Contains(name[0]))
            {
                prefix = prefix.Substring(0, prefix.Length - 1);
            }
        }

        //Change the ending to -ide if it is allowed
        if (changeEnding)
        {
            List<string> endings = new List<string>();
            endings.Add("ine");
            endings.Add("ogen");
            endings.Add("ygen");
            endings.Add("orous");
            endings.Add("on");
            endings.Add("ium");
            endings.Add("ur");

            foreach (string ending in endings)
            {
                if (name.EndsWith(ending))
                {
                    name = name.Replace(ending, "") + "ide";
                    break;
                }
            }
        }

        string completedStr = prefix+name;
        return char.ToUpper(completedStr[0]) + completedStr.Substring(1,completedStr.Length-1); //essentially just capitalizes the first letter
    }

    protected static int AtomsRequiredToBalance(int chargeRaw, int opposingCharge)
    {
        //Calculates the absolute values for the balancing check
        // (using the raw values will mess with the math and produce incorrect results, mainly in the case of negative and positive charges)
        int chargeAbs = Math.Abs(chargeRaw);
        int opposingAbs = Math.Abs(opposingCharge);

        //Return if it is impossible to balance
        if (Math.Max(chargeAbs, opposingAbs) % Math.Min(chargeAbs, opposingAbs) != 0) return -1;

        //Calculate how much atoms are required and then return that value
        int atomsRequired = 1;
        int charge = chargeRaw;
        while(charge + opposingCharge != 0)
        {
            atomsRequired++;
            charge += chargeRaw;
        }

        return atomsRequired;
    }
}

[Serializable]
public class IonicElement : BaseChemicalElement
{
    public static List<IonicElement> ionicElements { get; protected set; } = new List<IonicElement>();

    public int charge { get; private set; }
    public float electronegativity { get; private set; }

    public bool metal { get; private set; }

    public IonicElement(string name, string symbol, int charge, float negativity, bool metal = false) : base(name, symbol)
    {
        this.charge = charge;
        electronegativity = negativity;
        this.metal = metal;
    }

    string GetPrefix(int amount, IonicElement element)
    {
        //Return if the element is a metal, since metals don't have prefixes
        // (again, Lithium Sulfate. not Dilithium Sulfate. that's Star Trek dingus)
        if (element.metal) return string.Empty;

        string prefix;
        if (!prefixes.TryGetValue(amount, out prefix)) prefix = "(10+)";
        return prefix;
    }

    public Compound CovalentBond(Tuple<IonicElement, int> element)
    {
        //Calculate the charge of the element
        int elementCharge = element.Item2 * element.Item1.charge;

        //Calculate the amount of atoms this element will need, and exit if it is impossible to balance
        int atomAmount = AtomsRequiredToBalance(charge, elementCharge);
        if (atomAmount == -1) return null;
        Tuple<IonicElement, int> currentElement = new Tuple<IonicElement, int>(this, atomAmount);

        //Get the first element by sorting the electronegativity values
        // (the element with the higher electronegativity is the one that goes 2nd)
        Tuple<IonicElement, int> electronegative = (electronegativity > element.Item1.electronegativity) ? currentElement : element;
        Tuple<IonicElement, int> electropositive = (electronegative.Item1 == this) ? element : currentElement;

        //Draft the electropositve element's prefix, but ONLY if it is greater than one
        string electropositivePrefix = string.Empty;
        if(electropositive.Item2 > 1)
        {
            electropositivePrefix = GetPrefix(electropositive.Item2, electropositive.Item1);
        }

        //Get the prefix for the electronegative compound
        string electronegativePrefix = GetPrefix(electronegative.Item2, electronegative.Item1);

        //Create the compound name
        string name = ElementNameToIon(electropositive.Item1.name, electropositivePrefix, false) + " " + ElementNameToIon(electronegative.Item1.name, electronegativePrefix);

        //Create the compound and return it
        return new Compound(name, new Dictionary<string, int>() { { electropositive.Item1.symbol, electropositive.Item2 }, { electronegative.Item1.symbol, electronegative.Item2 } });
    }
}

[Serializable]
public class TransitionElement : BaseChemicalElement
{
    public static List<TransitionElement> transitionElements { get; protected set; } = new List<TransitionElement>();
    public List<int> charges { get; private set; } = new List<int>();

    public TransitionElement(string name, string symbol, int[] charges) : base(name, symbol)
    {
        this.charges = charges.ToList();
        this.charges.Sort();
    }

    public Compound Bond(Tuple<IonicElement, int> element)
    {
        //Calculate the total required charge
        int chargeCalculated = element.Item2 * element.Item1.charge;

        //Loop through all the charges to find a match
        int atoms = 0;
        int charge = 0;
        foreach (int elementCharge in charges)
        {
            int atomsRequired = AtomsRequiredToBalance(elementCharge, chargeCalculated);
            if(atomsRequired != -1)
            {
                atoms = atomsRequired;
                charge = elementCharge;
                break;
            }
        }

        //Exit if there are no matches; return the compound if there are
        if (atoms == 0 && charge == 0) return null;
        else
            //I subtract "1" from charge since we're dealing with indexes here.
            return new Compound(name + " (" + ChemicalGenUtils.RomanNumerals[charge-1] + ") " + ElementNameToIon(element.Item1.name),
                new Dictionary<string, int>() { { symbol, atoms }, { element.Item1.symbol, element.Item2 } });
    }
}

[Serializable]
public class Compound
{
    public string name { get; private set; }
    public string chemicalFormat { get; private set; }

    /// <summary>
    /// Generates a chemical compound
    /// </summary>
    /// <param name="name">The compound name</param>
    /// <param name="symbols">A dictionary which specifies a string (the element) and how much of the element is within the compound</param>
    public Compound(string name, Dictionary<string, int> symbols)
    {
        this.name = name;

        foreach(KeyValuePair<string, int> symbol in symbols)
        {
            chemicalFormat += symbol.Key + ChemicalGenUtils.Superscript(symbol.Value);
        }
    }
}

public static class ElementalCompoundGenerator
{
    enum ElementType { Ionic, Transition };

    //Function that initializes the periodic table textassets
    public static void Init()
    {
        foreach(TextAsset asset in Resources.LoadAll<TextAsset>("PeriodicElementInitialization"))
        {
            ElementType? type = null;
            foreach(string element in asset.text.Split('\n'))
            {
                string identifier = "Element-Type:";

                //Get the element type from the identifier, and then continue to the elements
                if (element.StartsWith(identifier))
                {
                    type = (ElementType)System.Enum.Parse(typeof(ElementType), element.Replace(identifier, string.Empty));
                    continue;
                }

                //Split the element again to get all it's values
                string[] data = element.Split('|');

                //Get the shared data types and set them as variables
                string name = data[0];
                string symbol = data[1];

                //Construct the class and then add it to the main elements list
                switch (type)
                {
                    case ElementType.Ionic:
                        //If the element is specified to be a metal, the program will make wind of that
                        bool metal = false;
                        if (data.Length > 4) metal = bool.Parse(data[4]);

                        IonicElement.ionicElements.Add(new IonicElement(name, symbol, int.Parse(data[2]), float.Parse(data[3]), metal));
                        break;
                    case ElementType.Transition:
                        //Turn the comma-seperated values into an int array
                        List<int> array = new List<int>();
                        foreach(string s in data[2].Split(','))
                        {
                            array.Add(int.Parse(s));
                        }

                        //Add the array into the transition elements class
                        TransitionElement.transitionElements.Add(new TransitionElement(name, symbol, array.ToArray()));
                        break;
                }
            }
        }
    }

    public static Compound GenerateRandomCompound(int seed)
    {
        //Generate a random number generator instance
        System.Random random = new System.Random(seed);

        //Determine whether to spawn a transitional metal element or a covalent one
        // true = transitional, false = covalent
        BaseChemicalElement element = null;
        List<int> charges = new List<int>();
        switch(random.Next(0, 101) > 50)
        {
            case true:
                TransitionElement receivedElement = TransitionElement.transitionElements[random.Next(0, TransitionElement.transitionElements.Count)];
                charges = receivedElement.charges;
                element = receivedElement;
                break;
            case false:
                IonicElement ionicElement = IonicElement.ionicElements[random.Next(0, IonicElement.ionicElements.Count)];
				
				//Exit immediately and return the element as a compound if the substance has a charge of 0
				if(ionicElement.charge == 0) return new Compound(ionicElement.name, new Dictionary<string, int>(){ {ionicElement.symbol, 1} });
				
                charges = new List<int>() { ionicElement.charge };
                element = ionicElement;
                break;
        }

        //Generate an array of possible elements to pair with
        IonicElement[] possiblePairs = IonicElement.ionicElements.FindAll((possible) =>
        {
            //Immediately exit if it is the same element to the one we're comparing it with.
            if (possible == element) return false;
            
            //Loop through the possible charges to see if they cancel out. If they do, return true.
            foreach(int charge in charges)
            {
                if (possible.charge + charge == 0) return true;
            }

            //Return false otherwise
            return false;
        }).ToArray();

        //Exit if the array is empty.
        if (possiblePairs.Length == 0) return null;

        //Get a random value out of the array, or the first element if it is only filled with one
        IonicElement elementPair = possiblePairs[0];
        if(possiblePairs.Length > 1)
        {
            elementPair = possiblePairs[random.Next(0, possiblePairs.Length)];
        }

        //Set the amount of atoms
        Tuple<IonicElement, int> atoms = new Tuple<IonicElement, int>(elementPair, random.Next(1, 4));

        //Instruct the first element to start building the compound
        if(element is TransitionElement)
        {
            return ((TransitionElement)element).Bond(atoms);
        }
        else
        {
            return ((IonicElement)element).CovalentBond(atoms);
        }
    }
}