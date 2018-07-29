using System;
using System.Linq;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;

public class Player
{
    private string description;
    /// <summary>
    /// Player health
    /// </summary>
    int health;
    /// <summary>
    /// Player max mana
    /// </summary>
    public int mana;
    /// <summary>
    /// Card count in the deck
    /// </summary>
    int deck;
    int rune;
    public Player(string description)
    {
        Game.Debug(description);

        this.description = description;
        
        var tokens = description.Split(' ');
        this.health = int.Parse(tokens[0]);
        this.mana = int.Parse(tokens[1]);
        this.deck = int.Parse(tokens[2]);
        this.rune = int.Parse(tokens[3]);           
    }
    public Player Clone()
    {
        return new Player(this.description);
    }
    public void Summon(Card card)
    {
        this.mana = this.mana - card.cost;
    }
    public bool CanSummon(Card card)
    {
        return card.cost <= this.mana; 
    }
    
}
#region Commands
public abstract class Command
{
}
public class PassCommand : Command
{
    public override string ToString()
    {
        return "PASS";
    }
}
public class PickCommand : Command
{
    private readonly int nb;
    public PickCommand(int nb)
    {
        this.nb = nb;
    }
    public override string ToString()
    {
        return $"PICK {nb.ToString()}";
    }
}

public class SummonCommand : Command
{
    private readonly int creatureId;
    public SummonCommand(Card card)
    {
        this.creatureId = card.instanceId;
    }
    public override string ToString()
    {
        return $"SUMMON {creatureId.ToString()}";
    }
}
public class AttackCreatureCommand : Command
{
    private readonly int fromCreatureId;
    private readonly int targetCreatureId;
    public AttackCreatureCommand(int fromCreatureId, int targetCreatureId)
    {
        this.fromCreatureId = fromCreatureId;
        this.targetCreatureId = targetCreatureId;
    }
    public override string ToString()
    {
        return $"ATTACKE {fromCreatureId.ToString()} {targetCreatureId.ToString()}";
    }
}

public class AttackOpponentCommand : Command
{
    private readonly int fromCreatureId;
    public AttackOpponentCommand(Card fromCard)
    {
        this.fromCreatureId = fromCard.instanceId;
    }
    public override string ToString()
    {
        return $"ATTACK {fromCreatureId.ToString()} -1";
    }
}
#endregion
public enum Location
{
    MyHand = 0,
    PlayerSide = 1,
    OpponentSide = -1
}
public class Card
{
    public readonly Location location;
    public readonly int instanceId;
    public readonly int attack;
    public readonly int cost;
    public readonly int myHealthChange;
    public readonly int opponentHealthChange;
    public Card(string description)
    {
        var inputs = description.Split(' ');
        int cardNumber = int.Parse(inputs[0]);
        this.instanceId = int.Parse(inputs[1]);
        this.location = (Location)int.Parse(inputs[2]);
        int cardType = int.Parse(inputs[3]);
        this.cost = int.Parse(inputs[4]);
        this.attack = int.Parse(inputs[5]);
        int defense = int.Parse(inputs[6]);
        string abilities = inputs[7];
        this.myHealthChange = int.Parse(inputs[8]);
        this.opponentHealthChange = int.Parse(inputs[9]);
        int cardDraw = int.Parse(inputs[10]);
    }

    public int ComputeScoreForDraft()
    {
        return this.attack + this.myHealthChange - this.opponentHealthChange;
    }
}

public interface IDraftAI
{
    Command GetCommand();
}
public class DraftAI : IDraftAI
{
    private readonly Card[] cards;
    public DraftAI(Card[] cards)
    {
        this.cards = cards;
    }
    public Command GetCommand()
    {
        var bestCardScore = int.MinValue;
        var bestCardIndex = 0;

        for(var i = 0; i < cards.Length; i++)
        {
            var currentCard = cards[i];
            var currentScore = currentCard.ComputeScoreForDraft();
            if(currentScore > bestCardScore)
            {
                bestCardIndex = i;
                bestCardScore = currentScore;
            }
        }

        return new PickCommand(bestCardIndex);
    }
}
public interface AbstractBattleAI
{
    Command[] GetCommands();
}

public class BattleAI : AbstractBattleAI
{
    private readonly Player me;
    private readonly Player opponent;
    private readonly Card[] cards;
    
    public BattleAI(Player me, Player opponent, Card[] cards)
    {
        this.me = me;
        this.opponent = opponent;
        this.cards = cards;
    }
    public Command[] GetCommands()
    {
        var commands = new List<Command>();

        commands.AddRange(SummonCreatures());
        commands.AddRange(AttackOpponent());

        return commands.ToArray();
    }

    private List<AttackOpponentCommand> AttackOpponent()
    {
        return this.cards
                .Where(c => c.location == Location.PlayerSide)
                .Select( c => new AttackOpponentCommand(c))
                .ToList();              
    }

    private List<SummonCommand> SummonCreatures()
    {
        var commands = new List<SummonCommand>();
        var me = this.me.Clone();
        var myCardsInMyHand = this.cards
                        .Where(c => c.location == Location.MyHand)
                        .OrderByDescending(c => c.attack)
                        .ToArray();

        foreach(var cardInMyHand in myCardsInMyHand)
        {
            if(me.CanSummon(cardInMyHand))
            {
                me.Summon(cardInMyHand);
                commands.Add(new SummonCommand(cardInMyHand));
            }
        }
        return commands;
    }
}




/**
 * Auto-generated code below aims at helping you parse
 * the standard input according to the problem statement.
 **/
public class Game
{
    public static void Debug(string message)
    {
        Console.Error.WriteLine(message);
    }
    static void Main(string[] args)
    {
        // game loop
        while (true)
        {
            var me = new Player(Console.ReadLine());
            var opponent  = new Player(Console.ReadLine());

            var isDraftPhase = me.mana == 0;

            int opponentHand = int.Parse(Console.ReadLine());
            int cardCount = int.Parse(Console.ReadLine());

            Card[] cards = new Card[cardCount];

            for (int i = 0; i < cardCount; i++)
            {
                var description = Console.ReadLine();
                Game.Debug(description);
                cards[i] = new Card(description);
            }

            if(isDraftPhase)
            {
                var draftAI = new DraftAI(cards);
                var command = draftAI.GetCommand();
                Console.WriteLine(command.ToString());
            }
            else
            {
                var battleAI = new BattleAI(me, opponent, cards);
                var commands = battleAI.GetCommands();

                Console.WriteLine(string.Join(";", commands.Select(c => c.ToString()).ToArray()));
            }
        }
    }
}