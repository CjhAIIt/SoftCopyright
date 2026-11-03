const express = require('express');

const app = express();
const port = process.env.PORT || 3000;

app.get('/', (req, res) => {
  res.send('样例服务已启动');
});

app.listen(port, () => {
  console.log(`样例服务监听 ${port}`);
});
