using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static Slime;

public class GameManager : MonoBehaviour
{
    public static GameManager Active;

    HexBoard hexBoard;
    Slime slime;

    public int CurrentDifficultyLevel;
    public int TurnNumber;
    public int StartingResources = 5;
    public int Score = 0;

    const int spread_score = 50;
    const int resource_absorb_score = 10;
    const int goal_spread_score = 250;
    const int upgrade_unlock_score = 75;

    public int NumberOfGoals = 1;

    private int goalsReached = 0;

    public TurnState CurrentState;

    [SerializeField] TextMeshProUGUI txtMoisture;
    [SerializeField] TextMeshProUGUI txtNutrients;
    [SerializeField] TextMeshProUGUI txtOxygen;
    [SerializeField] TextMeshProUGUI txtTurn;
    [SerializeField] TextMeshProUGUI txtGoals;
    [SerializeField] TextMeshProUGUI txtScore;

    [SerializeField] TextMeshProUGUI txtSpreadMoistureCost;
    [SerializeField] TextMeshProUGUI txtSpreadNutrientsCost;
    [SerializeField] TextMeshProUGUI txtSpreadOxygenCost;

    [SerializeField] GameObject panelPaper;
    [SerializeField] GameObject panelConfirmSpread;
    [SerializeField] GameObject panelAbilities;
    [SerializeField] GameObject panelGameOver;

    [SerializeField] Button btnSpread;

    [SerializeField] List<Upgrade> SlimeUpgrades;
    [SerializeField] AudioSource gameAudio;

    public int SpreadMoistureCost;
    public int SpreadNutrientsCost;
    public int SpreadOxygenCost;

    void OnEnable()
    {
        Active = this;
    }

    // Start is called before the first frame update
    void Start()
    {
        gameAudio.volume = Utils.MasterVolume;

        hexBoard = GetComponent<HexBoard>();
        slime = GetComponent<Slime>();

        hexBoard.GenerateHexBoard(NumberOfGoals);
        CurrentDifficultyLevel = 0;
        TurnNumber = 0;
        CurrentState = TurnState.Idle;

        slime.MoistureCount = StartingResources;
        slime.NutrientCount = StartingResources;
        slime.OxygenCount = StartingResources;

        UpdateResourceUI();
    }

    public void PlaceSlime(Hex startingTile)
    {
        /*int middleRange = hexBoard.BoardRadius / 5;

        int q = Random.Range(-middleRange, middleRange);
        int r = Random.Range(-middleRange, middleRange);
        Hex startingTile = HexBoard.Active.Hexes[new HexCoordinates(q, r)];*/
        slime.OccupyHex(startingTile, null);
        UpdateResourceUI();
    }

    public void UpgradeButtonClick()
    {
        CurrentState = TurnState.ChoosingUpgrade;
    }

    public void SpreadButtonClick()
    {
        CurrentState = TurnState.SpreadingToHex;

        List<Hex> spreadableHexes = new List<Hex>();

        foreach (Hex hex in slime.occupiedSpaces)
        {
            spreadableHexes.AddRange(hex.FindNeighbors().Where(h => h != null && !h.IsOccupied).Except(spreadableHexes));
        }

        if (slime.UpgradeStatus[Slime.Upgrades.SendSpores])
        {
            List<Hex> tempList = new List<Hex>();
            foreach (Hex hex in spreadableHexes)
            {
                tempList.AddRange(hex.FindNeighbors().Where(h => h != null && !h.IsOccupied).Except(spreadableHexes).Except(tempList));
            }
            spreadableHexes.AddRange(tempList);
        }

        hexBoard.SpreadableHexes = spreadableHexes;

        foreach (Hex hex in spreadableHexes)
        {
            hex.HighlightSpreadable();
        }
    }

    public void ConfirmSpreadClick(Hex hex)
    {
        if (hex != null)
        {
            Hex hexBridgeFrom = null;
            for (int i = slime.occupiedSpaces.Count - 1; i >= 0; i--)
            {
                if (slime.occupiedSpaces[i].FindNeighbors().Contains(hex))
                {
                    hexBridgeFrom = slime.occupiedSpaces[i];
                    break;
                }
            }

            slime.OccupyHex(hex, hexBridgeFrom);

            Score += spread_score;

            if (hex.IsGoal)
            {
                goalsReached += 1;
                Score += goal_spread_score;
                if (goalsReached == NumberOfGoals)
                {
                    WinAndReset();
                }
                txtGoals.text = System.String.Format("{0}/{1}", goalsReached, NumberOfGoals);
            }

            //StartCoroutine(Occupy(hex, hexBridgeFrom));

            if (slime.UpgradeStatus[Upgrades.ExtraHexSpore])
            {
                List<Hex> neightbors = hex.FindNeighbors().Where(h => h != null && !h.IsOccupied).ToList();

                if (neightbors.Count > 0)
                {
                    //StartCoroutine(Occupy(neightbors[Random.Range(0, neightbors.Count)], hex));
                    Hex hex2 = neightbors[UnityEngine.Random.Range(0, neightbors.Count)];
                    slime.OccupyHex(hex2, hex);
                    Score += spread_score;

                    if (hex2.IsGoal)
                    {
                        goalsReached += 1;
                        Score += goal_spread_score;
                        if (goalsReached == NumberOfGoals)
                        {
                            WinAndReset();
                        }
                        txtGoals.text = System.String.Format("{0}/{1}", goalsReached, NumberOfGoals);
                    }
                }
            }

            bool hasSpreadCostUpgrade = slime.UpgradeStatus[Slime.Upgrades.DiscountSpreading];
            slime.MoistureCount -= (hasSpreadCostUpgrade ? ((int)(SpreadMoistureCost / 2)) : SpreadMoistureCost);
            slime.NutrientCount -= (hasSpreadCostUpgrade ? ((int)(SpreadNutrientsCost / 2)) : SpreadNutrientsCost);
            slime.OxygenCount -= (hasSpreadCostUpgrade ? ((int)(SpreadOxygenCost / 2)) : SpreadOxygenCost);

            if (slime.MoistureCount >= SpreadMoistureCost && slime.NutrientCount >= SpreadNutrientsCost && slime.OxygenCount >= SpreadOxygenCost)
            {
                btnSpread.interactable = true;
            }
            else
            {
                btnSpread.interactable = false;
            }

            GoBackToIdleState();
            ClearSpreadableDisplay();
            UpdateResourceUI();
        }
    }

    IEnumerator Occupy(Hex to, Hex from)
    {
        slime.OccupyHex(to, from);

        yield return new WaitForSeconds(2f);

        if (slime.UpgradeStatus[Upgrades.ExtraHexSpore])
        {
            List<Hex> neightbors = to.FindNeighbors().Where(h => h != null && !h.IsOccupied).ToList();

            if (neightbors.Count > 0)
            {
                //StartCoroutine(Occupy(neightbors[Random.Range(0, neightbors.Count)], hex));
                slime.OccupyHex(neightbors[UnityEngine.Random.Range(0, neightbors.Count)], to);
            }
        }
    }

    public void WinAndReset()
    {

        foreach (GameObject slime in GameObject.FindGameObjectsWithTag("Slime"))
        {
            Destroy(slime);
        }

        foreach (GameObject bridge in GameObject.FindGameObjectsWithTag("SlimeBridge"))
        {
            Destroy(bridge);
        }

        CurrentState = TurnState.Idle;
        ClearSpreadableDisplay();
        slime.occupiedSpaces = new List<Hex>();


        NumberOfGoals = NumberOfGoals + 1;
        goalsReached = 0;
        hexBoard.ResetBoard(NumberOfGoals);
        CurrentDifficultyLevel++;
        TurnNumber = 0;


        slime.MoistureCount = StartingResources;
        slime.NutrientCount = StartingResources;
        slime.OxygenCount = StartingResources;

        foreach (Upgrades upgrade in Enum.GetValues(typeof(Upgrades)))
        {
            slime.UpgradeStatus[upgrade] = false;
        }

        foreach (Upgrade upgrade in SlimeUpgrades)
        {
            upgrade.Reset();
        }

        UpdateResourceUI();

    }

    public void ClearSpreadableDisplay()
    {
        foreach (Hex hex in hexBoard.SpreadableHexes)
        {
            hex.UnHighlight();
        }
        hexBoard.SpreadableHexes.Clear();
        HideConfirmSpreadPanel();
    }

    public void GoBackToIdleState()
    {
        CurrentState = TurnState.Idle;
    }

    public void EndTurnButtonClick()
    {
        int resourceCount = slime.OxygenCount + slime.NutrientCount + slime.MoistureCount;
        CurrentState = TurnState.EndOfTurn;
        slime.OnTurnEnd();

        Score += (slime.OxygenCount + slime.NutrientCount + slime.MoistureCount - resourceCount) * resource_absorb_score;

        if (slime.MoistureCount >= SpreadMoistureCost && slime.NutrientCount >= SpreadNutrientsCost && slime.OxygenCount >= SpreadOxygenCost)
        {
            btnSpread.interactable = true;
        }
        else
        {
            btnSpread.interactable = false;
        }

        CurrentState = TurnState.StartOfTurn;
        slime.OnTurnStart();

        TurnNumber++;

        UpdateResourceUI();

        CurrentState = TurnState.Idle;
    }

    public void GameOver()
    {
        panelAbilities.SetActive(false);
        panelGameOver.SetActive(true);
    }

    public void MainMenu()
    {
        SceneManager.LoadScene(0, LoadSceneMode.Single);
    }

    public void UpdateResourceUI()
    {
        int moisture = 0;
        int nutrients = 0;
        int oxygen = 0;

        bool hasDrainUpgrade = slime.UpgradeStatus[Slime.Upgrades.ResourceDrainer];

        foreach (Hex hex in slime.occupiedSpaces)
        {
            moisture += hex.AbsorbMoisture(hasDrainUpgrade, true);
            nutrients += hex.AbsorbNutrients(hasDrainUpgrade, true);
            oxygen += hex.AbsorbOxygen(hasDrainUpgrade, true);
        }

        if (slime.UpgradeStatus[Slime.Upgrades.MoistureConserver])
        {
            moisture -= slime.occupiedSpaces.Count / 2;
        }
        else
        {
            moisture -= slime.occupiedSpaces.Count;
        }

        txtMoisture.text = slime.MoistureCount + "(" + (moisture >= 0 ? "+" : "") + moisture + ")";
        txtNutrients.text = slime.NutrientCount + "(" + (nutrients >= 0 ? "+" : "") + nutrients + ")";
        txtOxygen.text = slime.OxygenCount + "(" + (oxygen >= 0 ? "+" : "") + oxygen + ")";

        bool hasSpreadCostUpgrade = slime.UpgradeStatus[Slime.Upgrades.DiscountSpreading];

        txtSpreadMoistureCost.text = "Moisture Cost: " + (hasSpreadCostUpgrade ? ((int)(SpreadMoistureCost / 2)) : SpreadMoistureCost);
        txtSpreadNutrientsCost.text = "Nutrients Cost: " + (hasSpreadCostUpgrade ? ((int)(SpreadNutrientsCost / 2)) : SpreadNutrientsCost);
        txtSpreadOxygenCost.text = "Oxygen Cost: " + (hasSpreadCostUpgrade ? ((int)(SpreadOxygenCost / 2)) : SpreadOxygenCost);
        txtTurn.text = TurnNumber.ToString();
        txtScore.text = Score.ToString();

        CheckUpgradeCosts();
    }

    public void CheckUpgradeCosts()
    {
        foreach (Upgrade upgrade in SlimeUpgrades)
        {
            upgrade.UpdateUnlockability(slime.MoistureCount >= upgrade.MoistureCost && slime.NutrientCount >= upgrade.NutrientsCost && slime.OxygenCount >= upgrade.OxygenCost);
        }
    }

    public void UnlockUpgrade(Upgrades upgrade, int moisture, int nutrients, int oxygen)
    {
        slime.UpgradeStatus[upgrade] = true;
        Score += upgrade_unlock_score;

        slime.MoistureCount -= moisture;
        slime.NutrientCount -= nutrients;
        slime.OxygenCount -= oxygen;

        UpdateResourceUI();
    }

    public void RevealGoals()
    {
        foreach (Hex hex in hexBoard.Hexes.Values.Where(h => h.IsGoal))
        {
            hex.RevealFogOfWar();
        }
    }

    public void HideConfirmSpreadPanel()
    {
        panelPaper.gameObject.SetActive(true);
        panelConfirmSpread.gameObject.SetActive(false);
    }

    public enum TurnState
    {
        Idle = 0,
        ChoosingUpgrade = 1,
        SpreadingToHex = 2,
        StartOfTurn = 3,
        EndOfTurn = 4
    }
}
