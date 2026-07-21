import CreateSessionForm from '../components/CreateSessionForm'
import ThemeToggle from '../components/ThemeToggle'

function HomePage() {
  return (
    <section className="mx-auto flex max-w-md flex-col gap-6 px-4 py-12">
      <ThemeToggle />
      <h1>TrashAnimal</h1>
      <p>Start a new game and invite friends to join.</p>
      <CreateSessionForm />
    </section>
  )
}

export default HomePage
