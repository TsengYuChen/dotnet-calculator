import { createApp } from 'vue'
import 'element-plus/theme-chalk/base.css'
// Function APIs are not covered by the component resolver: their styles are loaded here.
import 'element-plus/es/components/message/style/css'
import 'element-plus/es/components/message-box/style/css'
import App from './App.vue'
import { setUnauthorizedHandler } from './api/client'
import { redirectToLogin, router } from './router'
import './style.css'

setUnauthorizedHandler(redirectToLogin)

createApp(App).use(router).mount('#app')
