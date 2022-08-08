using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public class ResultWindowDisplay : MonoBehaviour
{
    [Header("References")]
    public Text reward_xp; //1000 xp for one rank up
    public Text base_money; //completing a game, +25
    public Text win_money; //won the game, + 175
    public Text kill_bonus; //Each kill is additional x25
    public Text destroy_bonus; //Each host machine destroy additional x25
    public Text total_payment;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void Display(int xp, int baseMoney, int winMoney, int killBonus, int destroyBonus, int totalPay)
    {
        reward_xp.text = "+ " + xp+"xp";
        base_money.text = "$" + baseMoney;
        win_money.text = "$" + winMoney;
        kill_bonus.text = "$" + killBonus;
        destroy_bonus.text = "$" + destroyBonus;
        total_payment.text = "$" + totalPay;

    }
    
}
