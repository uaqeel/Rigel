# Rigel

Small Winforms app that tracks crypto futures basis on the FTX exchange in real-time. Alongside charting the basis for multiple futures, the app calculates implied interest rates for each future and from these constructs the implied yield curve and forward rates for different coins.

Written by Uzair Aqeel, contact at uzair@nairang.org.

# How to Use

Download and unpack the project. Build if necessary, then launch Rigel.exe.

The *History (days)* parameter controls how many days of historical data, both price and yields, is downloaded from FTX. The default 10 days is appropriate in order to keep the graphs legible.

The *Refresh rate (s)* parameter controls how frequently data is updated from FTX. The default value of 60s (ie, once a minute) is appropriate in order not to overload the service.

You can set up your own API Key and API Secret on the FTX developers' website and use those but in most cases, it is enough to use the test key/secret I've put in there already. If you wish to use your own key/secret, save them in the App.config file so they are auto-loaded when the app starts.

Click the *Load* button to initialise the app and retrieve the list of coins from FTX. The *Highest Yielding Tokens* graph will be updated to show the coins with the highest implied yields. Type or select the desired coin in *Token*. The list of associated futures will be retrieved; select the ones you wish to use to examine. For example, for ETH select the PERP future as well as any/all quarterly futures.

The *Implied Yields* graph will now show a time series of each selected future; this time series shows the annualised implied yield based on the spot and future prices of the coin.

The *Implied Yield Curve & Forward Rates* graph will show the current implied yield of each future as well as the implied forward rates, ie the implied interest rate between maturities.

The *Accrued interest on Perpetual Future* graph shows the accruals on a long perp future position as funding windows elapse.