function getURLsAndErrors() {
    var obj = { userInput: $("#userInput").val() };
    $.ajax({
        type: "POST",
        url: "WebService1.asmx/GetURLsAndErrors",
        data: JSON.stringify(obj),
        contentType: "application/json; charset=utf-8",
        dataType: "json",
        success: function (msg) {
            $("#URLAndErrorMessage").html(msg.d);
        },
        error: function (msg) {
            alert(JSON.stringify(msg));
        }
    });
};

function suggestWords() {
    $.ajax({
        type: "POST",
        url: "WebService1.asmx/getSuggestedWords",
        data: JSON.stringify({ lettersTyped: $("#userInput").val() }),
        contentType: "application/json; charset=utf-8",
        dataType: "json",
        success: function (msg) {
            $("#userInput").autocomplete({
                source: msg.d
            })
        },
        error: function (msg) {
            alert(JSON.stringify(msg));
        }
    });
};

function showPlayerStats() {
    $.ajax({
        type: "POST",
        url: "WebService1.asmx/showPlayerStats",
        data: JSON.stringify({ playerName: $("#userInput").val() }),
        contentType: "application/json; charset=utf-8",
        dataType: "json",
        success: function (msg) {
            //if (msg.d != "") {
            //    alert(msg.d);
            //}
            $("#stats").html(msg.d);
        },
        error: function (msg) {
            alert(JSON.stringify(msg));
        }
    });
};