using System;
using UnityEngine;
using UniRx;
using UnityEngine.UI;

public class Game : MonoBehaviour
{
    public CompositeDisposable disposables = new CompositeDisposable(); // Required so that we can dispose of all added ( .AddTo(...) ) subscriptions in one go

    // Setup the main vars
    public IntReactiveProperty score = new IntReactiveProperty(0);
    public IntReactiveProperty level = new IntReactiveProperty(0);

    private ReactiveProperty<int> scoreDelayed, highscore;

    // Menu objects
    public GameObject mainMenu, endMenu, gameMenu;

    // Button objects
    public Button startButton, restartButton;

    // Text objects
    public Text scoreText, deltaScoreText, levelText;
    public Text endMenuScoreText, endMenuHighcoreText;

    
    // Scene objects & materials
    public GameObject redBlock;
    public GameObject[] blocks;
    public Material whiteMaterial, yellowMaterial;

    // Use this for initialization
    void Start () {
        // ----------------------------------
        // Activate mainMenu
        // ----------------------------------
        mainMenu.SetActive(true);
        endMenu.SetActive(false);
        gameMenu.SetActive(false);


        // ----------------------------------
        // Setup scoreDelayed + scoreDelta 
        // ----------------------------------

        // Score Delayed
        scoreDelayed = score.Throttle(TimeSpan.FromSeconds(1)).ToReactiveProperty();

        // Score Delta ( no need to have it as a private value since we're not gonna need to use it again in another function )
        var scoreDelta = score.Select(x => x - scoreDelayed.Value);

        // Change deltaScoreText and format it so that if the number is positive it has a "+" in front of it
        scoreDelta.SubscribeToText(deltaScoreText, x => (x > 0 ? ("+" + x) : x.ToString()));

        // Add animation everytime score delta changes
        scoreDelta.Subscribe(_ => AnimateObj(deltaScoreText.gameObject));
        
        scoreDelayed.Subscribe(delayedScore => {
            // Change text
            scoreText.text = delayedScore.ToString(); // You can also set it using scoreDelayed.SubscribeToText(scoreText);

            // Start animation
            AnimateObj(scoreText.gameObject);

            // Clear deltaScore ocne we've updated the score
            deltaScoreText.text = "";
        });

        // ----------------------------------
        // Setup highscore ( and save values )
        // ----------------------------------
        int lastHighScore = PlayerPrefs.GetInt("highscore");
        highscore = score.StartWith(lastHighScore).DistinctUntilChanged().Scan(int.MinValue, Math.Max).Do(x => PlayerPrefs.SetInt("highscore", x)).ToReactiveProperty();

        // ----------------------------------
        // Setup levels
        // ----------------------------------
        level.SubscribeToText(levelText, x => "Level " + x.ToString());

        // ----------------------------------
        // Button actions
        // ----------------------------------
        startButton.onClick.AddListener(StartGame);
        restartButton.onClick.AddListener(StartGame);
    }

    public void OnApplicationQuit() {
        disposables.Dispose();// Dispose of all subscriptions ( also disposes all future subscriptions added to this composite )
    }

    /// <summary>
    /// Start game ( when start or restart button is clicked )
    /// </summary>
    public void StartGame() {
        // ----------------------------------
        // Activate gameMenu
        // ----------------------------------
        mainMenu.SetActive(false);
        endMenu.SetActive(false);
        gameMenu.SetActive(true);

        // ----------------------------------
        // Reset score
        // ----------------------------------
        score.Value = 0;
        scoreDelayed.SetValueAndForceNotify(0); // Force notify so that delayed score updates instantly

        // ----------------------------------
        // Reset level
        // ----------------------------------
        level.Value = 1;

        // ----------------------------------
        SetupGameLogic();
    }
    
    /// <summary>d
    /// Setup of the whole game logic
    /// </summary>
    public void SetupGameLogic() {

        // ----------------------------------
        // Setup local variables
        // ----------------------------------
        int targetValue = 0;
        float currentValueFloat = 0f, speed = 1f;

        ReactiveProperty<int> currentValue = new ReactiveProperty<int>();

        // ----------------------------------
        // Update level speed and state everytime we have a new level
        // ----------------------------------
        level.Subscribe(x => {
            speed = 1.5f + x * 0.2f;

            // Reset the red cube position
            currentValueFloat = 0;
            currentValue.Value = 0;

            // Make sure we don't select same value as previously
            int newRandom = UnityEngine.Random.Range(0, blocks.Length);
            if (newRandom != targetValue)
                targetValue = newRandom;
            else
                targetValue = (newRandom+1) % blocks.Length;

            // Change all block materials
            for (int i = 0; i < blocks.Length; i++) {
                blocks[i].GetComponentInChildren<MeshRenderer>().sharedMaterial = i == targetValue ? yellowMaterial : whiteMaterial;
            }
        }).AddTo(disposables);


        // ----------------------------------
        // Setup gameplay
        // ----------------------------------
        Observable.EveryUpdate().Subscribe(x => {
            // Changes the currentFloat with speed
            currentValueFloat += speed * Time.smoothDeltaTime;
            // Reset its value once it reaches max value
            if (currentValueFloat > blocks.Length) currentValueFloat = 0;

            // Transform the float into int and move the box using the subscription
            currentValue.Value = Mathf.FloorToInt(currentValueFloat);

        }).AddTo(disposables);

        // ----------------------------------
        // Set red cube position everytime currentValue changes
        // ----------------------------------
        var redBlockTrans = redBlock.transform; // You can also move expensive getters outside the main function
        currentValue.Subscribe(x => {
            redBlockTrans.localPosition = new Vector3(x * 2, 1.2f, 0f);
        });

        // ----------------------------------
        // Handle clicks/taps
        // ----------------------------------
        TapStream().Subscribe(x => {
            if (currentValue.Value == targetValue) {
                score.Value++;
                level.Value++;
            } else {
                StopGame();
            }
        }).AddTo(disposables);

    }

    /// <summary>
    /// Executed when game ends ( e.g. mission failed )
    /// </summary>
    public void StopGame() {
        disposables.Clear(); // Remove all subscribers that were added to 'disposables' though AddTo(...);
        PlayerPrefs.Save(); // Save highscore

        // ----------------------------------
        // Set endMenu values for score
        // ----------------------------------
        endMenuScoreText.text = score.Value.ToString();
        endMenuHighcoreText.text = "BEST " + highscore.Value.ToString();

        // ----------------------------------
        // Activate endMenu
        // ----------------------------------
        mainMenu.SetActive(false);
        endMenu.SetActive(true);
        gameMenu.SetActive(false);
    }

    /// <summary>
    /// Input handling
    /// </summary>
    /// <returns></returns>
    public IObservable<Vector3> TapStream() {
        return Observable.EveryUpdate()
            .Where(_ => Input.GetMouseButtonDown(0))
            .Select(_ => Input.mousePosition);
    }

    /// <summary>
    /// Does this fancy bouncy text animation 
    /// </summary>
    /// <param name="go">GameObject to animate</param>
    public void AnimateObj(GameObject go) {
        LeanTween.scale(go, Vector3.one, 0.2f)
            .setFrom(Vector3.one*0.5f)
            .setEase(LeanTweenType.easeOutBack);
    }
}
